using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeleSharp.TL;
namespace TeleSharp.TL
{
	[TLObject(1244130093)]
    public class TLStatsGraphAsync : TLAbsStatsGraph
    {
        public override int Constructor
        {
            get
            {
                return 1244130093;
            }
        }

             public string Token {get;set;}


		public void ComputeFlags()
		{
			
		}

        public override void DeserializeBody(BinaryReader br)
        {
            Token = StringUtil.Deserialize(br);

        }

        public override void SerializeBody(BinaryWriter bw)
        {
			bw.Write(Constructor);
            StringUtil.Serialize(Token,bw);

        }
    }
}
