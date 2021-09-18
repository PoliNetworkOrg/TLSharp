﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeleSharp.TL;
using TeleSharp.TL.Account;
using TeleSharp.TL.Auth;
using TeleSharp.TL.Channels;
using TeleSharp.TL.Contacts;
using TeleSharp.TL.Help;
using TeleSharp.TL.Messages;
using TeleSharp.TL.Upload;
using TLSharp.Core.Auth;
using TLSharp.Core.Exceptions;
using TLSharp.Core.MTProto.Crypto;
using TLSharp.Core.Network;
using TLSharp.Core.Network.Exceptions;
using TLSharp.Core.Utils;
using TLAuthorization = TeleSharp.TL.Auth.TLAuthorization;

namespace TLSharp.Core
{
    public class TelegramClient : IDisposable
    {
        private MtProtoSender sender;
        private TcpTransport transport;
        private string apiHash = String.Empty;
        private int apiId = 0;
        private Session session;
        private List<TLDcOption> dcOptions;
        private TcpClientConnectionHandler handler;
        private DataCenterIPVersion dcIpVersion;

        public Session Session
        {
            get { return session; }
        }

        /// <summary>
        /// Creates a new TelegramClient
        /// </summary>
        /// <param name="apiId">The API ID provided by Telegram. Get one at https://my.telegram.org </param>
        /// <param name="apiHash">The API Hash provided by Telegram. Get one at https://my.telegram.org </param>
        /// <param name="store">An ISessionStore object that will handle the session</param>
        /// <param name="sessionUserId">The name of the session that tracks login info about this TelegramClient connection</param>
        /// <param name="handler">A delegate to invoke when a connection is needed and that will return a TcpClient that will be used to connect</param>
        /// <param name="dcIpVersion">Indicates the preferred IpAddress version to use to connect to a Telegram server</param>
        public TelegramClient(int apiId, string apiHash,
            ISessionStore store = null, string sessionUserId = "session", TcpClientConnectionHandler handler = null,
            DataCenterIPVersion dcIpVersion = DataCenterIPVersion.Default)
        {
            if (apiId == default(int))
                throw new MissingApiConfigurationException("API_ID");
            if (string.IsNullOrEmpty(apiHash))
                throw new MissingApiConfigurationException("API_HASH");

            if (store == null)
                store = new FileSessionStore();

            this.apiHash = apiHash;
            this.apiId = apiId;
            this.handler = handler;
            this.dcIpVersion = dcIpVersion;

            session = Session.TryLoadOrCreateNew(store, sessionUserId);
            transport = new TcpTransport(session.DataCenter.Address, session.DataCenter.Port, this.handler);
        }


        public async Task ConnectAsync(bool reconnect = false, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();

            if (session.AuthKey == null || reconnect)
            {
                var result = await Authenticator.DoAuthentication(transport, token).ConfigureAwait(false);
                session.AuthKey = result.AuthKey;
                session.TimeOffset = result.TimeOffset;
            }

            sender = new MtProtoSender(transport, session);

            //set-up layer
            var config = new TLRequestGetConfig();
            var request = new TLRequestInitConnection()
            {
                ApiId = apiId,
                AppVersion = "1.0.0",
                DeviceModel = "PC",
                LangCode = "en",
                Query = config,
                SystemVersion = "Win 10.0"
            };
            var invokewithLayer = new TLRequestInvokeWithLayer() { Layer = 66, Query = request };
            await sender.Send(invokewithLayer, token).ConfigureAwait(false);
            await sender.Receive(invokewithLayer, token).ConfigureAwait(false);

            dcOptions = ((TLConfig)invokewithLayer.Response).DcOptions.ToList();
        }

        private async Task ReconnectToDcAsync(int dcId, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();

            if (dcOptions == null || !dcOptions.Any())
                throw new InvalidOperationException($"Can't reconnect. Establish initial connection first.");

            TLExportedAuthorization exported = null;
            if (session.TLUser != null)
            {
                TLRequestExportAuthorization exportAuthorization = new TLRequestExportAuthorization() { DcId = dcId };
                exported = await SendRequestAsync<TLExportedAuthorization>(exportAuthorization, token).ConfigureAwait(false);
            }

            IEnumerable<TLDcOption> dcs;
            if (dcIpVersion == DataCenterIPVersion.OnlyIPv6)
                dcs = dcOptions.Where(d => d.Id == dcId && d.Ipv6); // selects only ipv6 addresses 	
            else if (dcIpVersion == DataCenterIPVersion.OnlyIPv4)
                dcs = dcOptions.Where(d => d.Id == dcId && !d.Ipv6); // selects only ipv4 addresses
            else
                dcs = dcOptions.Where(d => d.Id == dcId); // any

            dcs = dcs.Where(d => !d.MediaOnly);

            TLDcOption dc;
            if (dcIpVersion != DataCenterIPVersion.Default)
            {
                if (!dcs.Any())
                    throw new Exception($"Telegram server didn't provide us with any IPAddress that matches your preferences. If you chose OnlyIPvX, try switch to PreferIPvX instead.");
                dcs = dcs.OrderBy(d => d.Ipv6);
                dc = dcIpVersion == DataCenterIPVersion.PreferIPv4 ? dcs.First() : dcs.Last(); // ipv4 addresses are at the beginning of the list because it was ordered
            }
            else
                dc = dcs.First();

            var dataCenter = new DataCenter(dcId, dc.IpAddress, dc.Port);

            transport = new TcpTransport(dc.IpAddress, dc.Port, handler);
            session.DataCenter = dataCenter;

            await ConnectAsync(true, token).ConfigureAwait(false);

            if (session.TLUser != null)
            {
                TLRequestImportAuthorization importAuthorization = new TLRequestImportAuthorization() { Id = exported.Id, Bytes = exported.Bytes };
                var imported = await SendRequestAsync<TLAuthorization>(importAuthorization, token).ConfigureAwait(false);
                OnUserAuthenticated((TLUser)imported.User);
            }
        }

      
        private async Task RequestWithDcMigration(TLMethod request, CancellationToken token = default(CancellationToken))
        {
            if (sender == null)
                throw new InvalidOperationException("Not connected!");

            var completed = false;
            while (!completed)
            {
                try
                {
                    await sender.Send(request, token).ConfigureAwait(false);
                    await sender.Receive(request, token).ConfigureAwait(false);
                    completed = true;
                }
                catch (DataCenterMigrationException e)
                {
                    if (session.DataCenter.DataCenterId.HasValue &&
                        session.DataCenter.DataCenterId.Value == e.DC)
                    {
                        throw new Exception($"Telegram server replied requesting a migration to DataCenter {e.DC} when this connection was already using this DataCenter", e);
                    }

                    await ReconnectToDcAsync(e.DC, token).ConfigureAwait(false);
                    // prepare the request for another try
                    request.ConfirmReceived = false;
                }
            }
        }

        public bool IsUserAuthorized()
        {
            return session.TLUser != null;
        }

        public async Task<bool> IsPhoneRegisteredAsync(string phoneNumber, CancellationToken token = default(CancellationToken))
        {
            if (String.IsNullOrWhiteSpace(phoneNumber))
                throw new ArgumentNullException(nameof(phoneNumber));

            var authCheckPhoneRequest = new TLRequestCheckPhone() { PhoneNumber = phoneNumber };

            await RequestWithDcMigration(authCheckPhoneRequest, token).ConfigureAwait(false);

            return authCheckPhoneRequest.Response.PhoneRegistered;
        }

        public async Task<string> SendCodeRequestAsync(string phoneNumber, CancellationToken token = default(CancellationToken))
        {
            if (String.IsNullOrWhiteSpace(phoneNumber))
                throw new ArgumentNullException(nameof(phoneNumber));

            var request = new TLRequestSendCode() { PhoneNumber = phoneNumber, ApiId = apiId, ApiHash = apiHash };

            await RequestWithDcMigration(request, token).ConfigureAwait(false);

            return request.Response.PhoneCodeHash;
        }

        public async Task<TLUser> MakeAuthAsync(string phoneNumber, string phoneCodeHash, string code, CancellationToken token = default(CancellationToken))
        {
            if (String.IsNullOrWhiteSpace(phoneNumber))
                throw new ArgumentNullException(nameof(phoneNumber));

            if (String.IsNullOrWhiteSpace(phoneCodeHash))
                throw new ArgumentNullException(nameof(phoneCodeHash));

            if (String.IsNullOrWhiteSpace(code))
                throw new ArgumentNullException(nameof(code));

            var request = new TLRequestSignIn() { PhoneNumber = phoneNumber, PhoneCodeHash = phoneCodeHash, PhoneCode = code };

            await RequestWithDcMigration(request, token).ConfigureAwait(false);

            OnUserAuthenticated(((TLUser)request.Response.User));

            return ((TLUser)request.Response.User);
        }

        public async Task<TLPassword> GetPasswordSetting(CancellationToken token = default(CancellationToken))
        {
            var request = new TLRequestGetPassword();

            await RequestWithDcMigration(request, token).ConfigureAwait(false);

            return (TLPassword)request.Response;
        }

 



        public async Task<TLUser> MakeAuthWithPasswordAsync(TLPassword password, string password_str, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();

            byte[] password_Bytes = Encoding.UTF8.GetBytes(password_str);
            IEnumerable<byte> rv = password.CurrentSalt.Concat(password_Bytes).Concat(password.CurrentSalt);

            SHA256Managed hashstring = new SHA256Managed();
            var password_hash = hashstring.ComputeHash(rv.ToArray());

            var request = new TLRequestCheckPassword() { PasswordHash = password_hash };

            await RequestWithDcMigration(request, token).ConfigureAwait(false);

            OnUserAuthenticated((TLUser)request.Response.User);

            return (TLUser)request.Response.User;
        }

        public async Task<TLUser> SignUpAsync(string phoneNumber, string phoneCodeHash, string code, string firstName, string lastName, CancellationToken token = default(CancellationToken))
        {
            var request = new TLRequestSignUp() { PhoneNumber = phoneNumber, PhoneCode = code, PhoneCodeHash = phoneCodeHash, FirstName = firstName, LastName = lastName };

            await RequestWithDcMigration(request, token).ConfigureAwait(false);

            OnUserAuthenticated((TLUser)request.Response.User);

            return (TLUser)request.Response.User;
        }

        public async Task<T> SendRequestAsync<T>(TLMethod methodToExecute, CancellationToken token = default(CancellationToken))
        {
            await RequestWithDcMigration(methodToExecute, token).ConfigureAwait(false);

            var result = methodToExecute.GetType().GetProperty("Response").GetValue(methodToExecute);

            return (T)result;
        }

        internal async Task<T> SendAuthenticatedRequestAsync<T>(TLMethod methodToExecute, CancellationToken token = default(CancellationToken))
        {
            if (!IsUserAuthorized())
                throw new InvalidOperationException("Authorize user first!");

            return await SendRequestAsync<T>(methodToExecute, token)
                .ConfigureAwait(false);
        }


        public async Task<TLUser> AccountUpdateUsernameAsync(string username, CancellationToken token = default(CancellationToken))
        {
            var req = new TeleSharp.TL.Account.TLRequestUpdateUsername { Username = username };

            return await SendAuthenticatedRequestAsync<TLUser>(req, token)
                .ConfigureAwait(false);
        }



        public async Task<TeleSharp.TL.Messages.TLChatFull> Messages_getFullChat(long chatId, CancellationToken token = default(CancellationToken))
        {
            var req = new TeleSharp.TL.Messages.TLRequestGetFullChat { ChatId = (int)chatId };

            return await SendAuthenticatedRequestAsync<TeleSharp.TL.Messages.TLChatFull>(req, token)
                .ConfigureAwait(false);
        }

        public async Task<bool> ChannelsUpdateUsername(int channelID, long? accessHash, string to, CancellationToken token = default(CancellationToken))
        {
            TLAbsInputChannel channel = null;

            if (accessHash != null)
            {
                channel = new TLInputChannel()
                {
                    ChannelId = channelID,
                    AccessHash = accessHash.Value
                };
            }
            else
            {
                channel = new TLInputChannel()
                {
                    ChannelId = channelID
                };
            }

            
            var req = new TeleSharp.TL.Channels.TLRequestUpdateUsername { Username = to, Channel = channel};

            return await SendAuthenticatedRequestAsync<bool>(req, token)
                .ConfigureAwait(false);
        }

        public async Task<TLAffectedMessages> ChannelsDeleteMessageAsync(TLAbsInputChannel peer, TLVector<int> messageId, CancellationToken token = default(CancellationToken))
        {
            var req = new TeleSharp.TL.Channels.TLRequestDeleteMessages {  Channel = peer, Id = messageId };

            return await SendAuthenticatedRequestAsync<TLAffectedMessages>(req, token)
                .ConfigureAwait(false);
        }

        public async Task<TeleSharp.TL.Channels.TLChannelParticipant> ChannelsGetParticipant(TLAbsInputChannel channel, TLAbsInputUser user, CancellationToken token = default(CancellationToken))
        {
            var req = new TeleSharp.TL.Channels.TLRequestGetParticipant { Channel = channel, UserId = user };

            return await SendAuthenticatedRequestAsync<TeleSharp.TL.Channels.TLChannelParticipant>(req, token)
                .ConfigureAwait(false);
        }

        public async Task<TLAbsUpdates> UpgradeGroupIntoSupergroup(long? groupId, CancellationToken token = default(CancellationToken))
        {
            if (groupId == null)
                return null;

            var req = new TeleSharp.TL.Messages.TLRequestMigrateChat() { ChatId = Convert.ToInt32( groupId.Value) };

            return await SendAuthenticatedRequestAsync<TLAbsUpdates>(req, token)
                .ConfigureAwait(false);
        }

        public async Task<bool?> Channels_EditDescription(TLChannel tLChannel, string desc, CancellationToken token = default(CancellationToken))
        {
            if (tLChannel == null)
                return null;


            TLInputPeerChannel peer2 = new TLInputPeerChannel() { ChannelId = tLChannel.Id };
            if (tLChannel.AccessHash != null)
            {
                peer2.AccessHash = tLChannel.AccessHash.Value;
            }
            var req = new TeleSharp.TL.Messages.TLRequestEditChatAbout() { peer = peer2, about = desc};


            return await SendAuthenticatedRequestAsync<bool>(req, token)
                .ConfigureAwait(false);
        }

        public async Task<bool> AccountCheckUsernameAsync(string username, CancellationToken token = default(CancellationToken))
        {
            var req = new TeleSharp.TL.Account.TLRequestCheckUsername { Username = username };

            return await SendAuthenticatedRequestAsync<bool>(req, token)
                .ConfigureAwait(false);
        }

        public async Task<TLAbsUpdates> ChannelsInviteToChannel(TLAbsInputChannel channel, TLVector<TLAbsInputUser> users, CancellationToken token = default(CancellationToken))
        {
            var req = new TeleSharp.TL.Channels.TLRequestInviteToChannel { Channel = channel, Users = users };

            return await SendAuthenticatedRequestAsync<TLAbsUpdates>(req, token)
                .ConfigureAwait(false);
        }

        public async Task<TLAbsUpdates> ChannelsEditAdmin(TLAbsInputChannel channel, 
            TLAbsInputUser userId, TLAbsChannelParticipantRole role,
            CancellationToken token = default(CancellationToken))
        {
            var req = new TLRequestEditAdmin {  Channel = channel, UserId = userId, Role = role };

            return await SendAuthenticatedRequestAsync<TLAbsUpdates>(req, token)
                       .ConfigureAwait(false);
        }

        public async Task<bool> Messages_EditChatAdmin(long chatId,
            TLAbsInputUser user, bool isAdmin,
            CancellationToken token = default(CancellationToken))
        {
            var req = new TLRequestEditChatAdmin {  ChatId = (int)chatId, UserId= user, IsAdmin = isAdmin };

            return await SendAuthenticatedRequestAsync<bool>(req, token)
                       .ConfigureAwait(false);
        }

        public async Task<TLAffectedMessages> Messages_DeleteMessages(bool revoke, TLVector<int> tLVector, CancellationToken token = default(CancellationToken))
        {
            var request = new TeleSharp.TL.Messages.TLRequestDeleteMessages() { Revoke = true, Id = tLVector };

            return await SendAuthenticatedRequestAsync<TLAffectedMessages>(request, token).ConfigureAwait(false);
        }

        public async Task<TeleSharp.TL.Messages.TLChatFull> getFullChat(TLAbsInputChannel channel, CancellationToken token = default(CancellationToken))
        {
            var req = new TeleSharp.TL.Channels.TLRequestGetFullChannel {  Channel = channel};

            return await SendAuthenticatedRequestAsync<TeleSharp.TL.Messages.TLChatFull>(req, token)
                .ConfigureAwait(false);
        }

        public async Task<TLChats> Messages_GetAllChats(CancellationToken token = default(CancellationToken))
        {
            var req = new TeleSharp.TL.Messages.TLRequestGetAllChats() { ExceptIds = new TLVector<int>() { } };

            return await SendAuthenticatedRequestAsync<TLChats>(req, token)
                .ConfigureAwait(false);
        }

        public async Task<TLResolvedPeer> ResolveUsernameAsync(string usernameToResolve, CancellationToken token = default(CancellationToken))
        {
            if (usernameToResolve.StartsWith("@"))
                usernameToResolve = usernameToResolve.Substring(1);

            var req = new TLRequestResolveUsername() { Username = usernameToResolve };

            return await SendAuthenticatedRequestAsync<TLResolvedPeer>(req, token)
                .ConfigureAwait(false);
        }

        public async Task<TLAbsUpdates> SendMessageReactionAsync(TLAbsInputPeer peer, int messageId, string emoji, CancellationToken token = default(CancellationToken))
        {
            try
            {
                var req = new TeleSharp.TL.TL.Messages.TLSendMessageReaction() { Msg_Id = messageId, Flags = 0, ReactionEmoji = emoji, Peer = peer };

                return await SendAuthenticatedRequestAsync<TLAbsUpdates>(req, token)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task<TLImportedContacts> ImportContactsAsync(IReadOnlyList<TLInputPhoneContact> contacts, CancellationToken token = default(CancellationToken))
        {
            var req = new TLRequestImportContacts { Contacts = new TLVector<TLInputPhoneContact>(contacts)};

            return await SendAuthenticatedRequestAsync<TLImportedContacts>(req, token)
                .ConfigureAwait(false);
        }

        public async Task<bool> DeleteContactsAsync(IReadOnlyList<TLAbsInputUser> users, CancellationToken token = default(CancellationToken))
        {
            var req = new TLRequestDeleteContacts {Id = new TLVector<TLAbsInputUser>(users)};

            return await SendAuthenticatedRequestAsync<bool>(req, token)
                .ConfigureAwait(false);
        }

        public async Task<TLLink> DeleteContactAsync(TLAbsInputUser user, CancellationToken token = default(CancellationToken))
        {
            var req = new TLRequestDeleteContact {Id = user};

            return await SendAuthenticatedRequestAsync<TLLink>(req, token)
                .ConfigureAwait(false);
        }

        public async Task<TLContacts> GetContactsAsync(CancellationToken token = default(CancellationToken))
        {
            var req = new TLRequestGetContacts() { Hash = "" };

            return await SendAuthenticatedRequestAsync<TLContacts>(req, token)
                .ConfigureAwait(false);
        }

        public async Task<TLAbsUpdates> SendMessageAsync(TLAbsInputPeer peer, string message,
             CancellationToken token = default(CancellationToken), TLAbsReplyMarkup replyMarkup = null)
        {
            return await SendAuthenticatedRequestAsync<TLAbsUpdates>(
                    new TLRequestSendMessage()
                    {
                        Peer = peer,
                        Message = message,
                        RandomId = Helpers.GenerateRandomLong(), 
                        ReplyMarkup = replyMarkup
                    }, token)
                .ConfigureAwait(false);
        }

        public async Task<Boolean> SendTypingAsync(TLAbsInputPeer peer, CancellationToken token = default(CancellationToken))
        {
            var req = new TLRequestSetTyping()
            {
                Action = new TLSendMessageTypingAction(),
                Peer = peer
            };
            return await SendAuthenticatedRequestAsync<Boolean>(req, token)
                .ConfigureAwait(false);
        }

        public async Task<TLAbsDialogs> GetUserDialogsAsync(int offsetDate = 0, int offsetId = 0, TLAbsInputPeer offsetPeer = null, int limit = 100, CancellationToken token = default(CancellationToken))
        {
            if (offsetPeer == null)
                offsetPeer = new TLInputPeerSelf();

            var req = new TLRequestGetDialogs()
            { 
                OffsetDate = offsetDate, 
                OffsetId = offsetId, 
                OffsetPeer = offsetPeer, 
                Limit = limit
            };
            return await SendAuthenticatedRequestAsync<TLAbsDialogs>(req, token)
                .ConfigureAwait(false);
        }

        public async Task<TLAbsUpdates> Messages_SendMedia(TLAbsInputPeer peer, TLAbsInputMedia media,
            CancellationToken token = default(CancellationToken))
        {
            return await SendAuthenticatedRequestAsync<TLAbsUpdates>(new TLRequestSendMedia()
                {
                    RandomId = Helpers.GenerateRandomLong(),
                    Background = false,
                    ClearDraft = false,
                    Media = media,
                    Peer = peer
                }, token)
                .ConfigureAwait(false);
        }
        
        public async Task<TLAbsUpdates> SendUploadedPhoto(TLAbsInputPeer peer, TLAbsInputFile file, string caption, CancellationToken token = default(CancellationToken))
        {
            return await SendAuthenticatedRequestAsync<TLAbsUpdates>(new TLRequestSendMedia()
                {
                    RandomId = Helpers.GenerateRandomLong(),
                    Background = false,
                    ClearDraft = false,
                    Media = new TLInputMediaUploadedPhoto() { File = file, Caption = caption },
                    Peer = peer
                }, token)
                .ConfigureAwait(false);
        }

        public async Task<TLAbsUpdates> SendUploadedDocument(
            TLAbsInputPeer peer, TLAbsInputFile file, string caption, string mimeType, TLVector<TLAbsDocumentAttribute> attributes, CancellationToken token = default(CancellationToken))
        {
            return await SendAuthenticatedRequestAsync<TLAbsUpdates>(new TLRequestSendMedia()
                {
                    RandomId = Helpers.GenerateRandomLong(),
                    Background = false,
                    ClearDraft = false,
                    Media = new TLInputMediaUploadedDocument()
                    {
                        File = file,
                        Caption = caption,
                        MimeType = mimeType,
                        Attributes = attributes
                    },
                    Peer = peer
                }, token)
                .ConfigureAwait(false);
        }

        public async Task<TLFile> GetFile(TLAbsInputFileLocation location, int filePartSize, int offset = 0, CancellationToken token = default(CancellationToken))
        {
            TLFile result = await SendAuthenticatedRequestAsync<TLFile>(new TLRequestGetFile
                {
                    Location = location,
                    Limit = filePartSize,
                    Offset = offset
                }, token)
                .ConfigureAwait(false);
            return result;
        }

        public async Task SendPingAsync(CancellationToken token = default(CancellationToken))
        {
            await sender.SendPingAsync(token)
                .ConfigureAwait(false);
        }

        public async Task<TLAbsMessages> GetHistoryAsync(TLAbsInputPeer peer, int offsetId = 0, int offsetDate = 0, 
            int addOffset = 0, int limit = 100, int maxId = 0, int minId = 0, CancellationToken token = default(CancellationToken))
        {
            var req = new TLRequestGetHistory()
            {
                Peer = peer,
                OffsetId = offsetId,
                OffsetDate = offsetDate,
                AddOffset = addOffset,
                Limit = limit,
                MaxId = maxId,
                MinId = minId
            };
            return await SendAuthenticatedRequestAsync<TLAbsMessages>(req, token)
                .ConfigureAwait(false);
        }

        public async Task<TLAbsUpdates> Messages_CreateChat(string title, TLVector<TLAbsInputUser> membersToInvite,
            CancellationToken token = default(CancellationToken))
        {
            var req = new TLRequestCreateChat()
            {
                 Title = title,
                 Users = membersToInvite
            };
            
            return await SendAuthenticatedRequestAsync<TLAbsUpdates>(req, token)
                .ConfigureAwait(false);
        }
        
        public async Task<TLAuthorization> AuthImportBotAuthorization(string tokenBot,  
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var req = new TLRequestImportBotAuthorization()
            {
                ApiHash = this.apiHash,
                ApiId =  this.apiId,
                BotAuthToken =  tokenBot
            };
            
            await RequestWithDcMigration(req, cancellationToken).ConfigureAwait(false);

            var response = req.Response;
            var user = response?.User;
            if (user == null)
                return null;

            if (!(user is TLUser user2)) 
                return null;
            
            OnUserAuthenticated(user2);
            return req.Response;
        }

        /// <summary>
        /// Serch user or chat. API: contacts.search#11f812d8 q:string limit:int = contacts.Found;
        /// </summary>
        /// <param name="q">User or chat name</param>
        /// <param name="limit">Max result count</param>
        /// <returns></returns>
        public async Task<TLFound> SearchUserAsync(string q, int limit = 10, CancellationToken token = default(CancellationToken))
        {
            var r = new TeleSharp.TL.Contacts.TLRequestSearch
            {
                Q = q,
                Limit = limit
            };

            return await SendAuthenticatedRequestAsync<TLFound>(r, token)
                .ConfigureAwait(false);
        }

        private void OnUserAuthenticated(TLUser TLUser)
        {
            session.TLUser = TLUser;
            session.SessionExpires = int.MaxValue;

            session.Save();
        }

        public bool IsConnected
        {
            get
            {
                if (transport == null)
                    return false;
                return transport.IsConnected;
            }
        }

        public void Dispose()
        {
            if (transport != null)
            {
                transport.Dispose();
                transport = null;
            }
        }
    }
}
