using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeleSharp.TL;
namespace TeleSharp.TL.Messages
{
	[TLObject(136574537)]
    public class TLVotesList : TLObject
    {
        public override int Constructor
        {
            get
            {
                return 136574537;
            }
        }

             public int Flags {get;set;}
     public int Count {get;set;}
     public TLVector<TLAbsMessageUserVote> Votes {get;set;}
     public TLVector<TLAbsUser> Users {get;set;}
     public string NextOffset {get;set;}


		public void ComputeFlags()
		{
			
		}

        public override void DeserializeBody(BinaryReader br)
        {
            Flags = br.ReadInt32();
Count = br.ReadInt32();
Votes = (TLVector<TLAbsMessageUserVote>)ObjectUtils.DeserializeVector<TLAbsMessageUserVote>(br);
Users = (TLVector<TLAbsUser>)ObjectUtils.DeserializeVector<TLAbsUser>(br);
if ((Flags & 1) != 0)
NextOffset = StringUtil.Deserialize(br);
else
NextOffset = null;


        }

        public override void SerializeBody(BinaryWriter bw)
        {
			bw.Write(Constructor);
            bw.Write(Flags);
bw.Write(Count);
ObjectUtils.SerializeObject(Votes,bw);
ObjectUtils.SerializeObject(Users,bw);
if ((Flags & 1) != 0)
StringUtil.Serialize(NextOffset,bw);

        }
    }
}
