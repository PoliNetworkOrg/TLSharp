using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeleSharp.TL;
namespace TeleSharp.TL.Stats
{
	[TLObject(-1421720550)]
    public class TLRequestGetBroadcastStats : TLMethod
    {
        public override int Constructor
        {
            get
            {
                return -1421720550;
            }
        }

                public int Flags {get;set;}
        public bool Dark {get;set;}
        public TLAbsInputChannel Channel {get;set;}
        public Stats.TLBroadcastStats Response{ get; set;}


		public void ComputeFlags()
		{
			
		}

        public override void DeserializeBody(BinaryReader br)
        {
            Flags = br.ReadInt32();
Dark = (Flags & 1) != 0;
Channel = (TLAbsInputChannel)ObjectUtils.DeserializeObject(br);

        }

        public override void SerializeBody(BinaryWriter bw)
        {
			bw.Write(Constructor);
            bw.Write(Flags);

ObjectUtils.SerializeObject(Channel,bw);

        }
		public override void DeserializeResponse(BinaryReader br)
		{
			Response = (Stats.TLBroadcastStats)ObjectUtils.DeserializeObject(br);

		}
    }
}
