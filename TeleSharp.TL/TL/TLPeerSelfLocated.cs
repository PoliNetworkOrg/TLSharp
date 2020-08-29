using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeleSharp.TL;
namespace TeleSharp.TL
{
	[TLObject(-118740917)]
    public class TLPeerSelfLocated : TLAbsPeerLocated
    {
        public override int Constructor
        {
            get
            {
                return -118740917;
            }
        }

             public int Expires {get;set;}


		public void ComputeFlags()
		{
			
		}

        public override void DeserializeBody(BinaryReader br)
        {
            Expires = br.ReadInt32();

        }

        public override void SerializeBody(BinaryWriter bw)
        {
			bw.Write(Constructor);
            bw.Write(Expires);

        }
    }
}
