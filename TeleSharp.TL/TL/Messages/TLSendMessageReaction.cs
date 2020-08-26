using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeleSharp.TL.TL.Messages
{
    [TLObject(627641572)]
    public class TLSendMessageReaction : TLMethod
    {
        public override int Constructor
        {
            get
            {
                return 627641572;
            }
        }


        public int Flags { get; set; }
        public TLAbsInputPeer Peer { get; set; }
        public int Msg_Id { get; set; }
        public string ReactionEmoji { get; set; }

        public TLUpdates Response { get; set; }

        public override void DeserializeBody(BinaryReader br)
        {
            Flags = br.ReadInt32();
            Peer = (TLAbsInputPeer)ObjectUtils.DeserializeObject(br);
            Msg_Id = br.ReadInt32();
            ReactionEmoji = StringUtil.Deserialize(br);
        }

        public override void DeserializeResponse(BinaryReader stream)
        {
            Response = (TLUpdates)ObjectUtils.DeserializeObject(stream);
        }

        public void ComputeFlags()
        {
            Flags = 0;
            Flags = !string.IsNullOrEmpty(ReactionEmoji) ? (Flags | 1) : (Flags & ~1);
        }

        public override void SerializeBody(BinaryWriter bw)
        {
            bw.Write(Constructor);
            ComputeFlags();
            bw.Write(Flags);

            ObjectUtils.SerializeObject(Peer, bw);
            bw.Write(Msg_Id);
            StringUtil.Serialize(ReactionEmoji, bw);

        }
    }
}
