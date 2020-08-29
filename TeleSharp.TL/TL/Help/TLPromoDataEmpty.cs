using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeleSharp.TL;
namespace TeleSharp.TL.Help
{
	[TLObject(-1728664459)]
    public class TLPromoDataEmpty : TLAbsPromoData
    {
        public override int Constructor
        {
            get
            {
                return -1728664459;
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
