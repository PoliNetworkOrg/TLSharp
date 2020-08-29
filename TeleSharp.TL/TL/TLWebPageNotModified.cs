using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeleSharp.TL;
namespace TeleSharp.TL
{
	[TLObject(1930545681)]
    public class TLWebPageNotModified : TLAbsWebPage
    {
        public override int Constructor
        {
            get
            {
                return 1930545681;
            }
        }

             public int Flags {get;set;}
     public int? CachedPageViews {get;set;}


		public void ComputeFlags()
		{
			
		}

        public override void DeserializeBody(BinaryReader br)
        {
            Flags = br.ReadInt32();
if ((Flags & 1) != 0)
CachedPageViews = br.ReadInt32();
else
CachedPageViews = null;


        }

        public override void SerializeBody(BinaryWriter bw)
        {
			bw.Write(Constructor);
            bw.Write(Flags);
if ((Flags & 1) != 0)
bw.Write(CachedPageViews.Value);

        }
    }
}
