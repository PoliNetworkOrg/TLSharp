using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeleSharp.TL;
namespace TeleSharp.TL.Bots
{
	[TLObject(-2141370634)]
    public class TLRequestSetBotCommands : TLMethod
    {
        public override int Constructor
        {
            get
            {
                return -2141370634;
            }
        }

                public TLVector<TLBotCommand> Commands {get;set;}
        public bool Response{ get; set;}


		public void ComputeFlags()
		{
			
		}

        public override void DeserializeBody(BinaryReader br)
        {
            Commands = (TLVector<TLBotCommand>)ObjectUtils.DeserializeVector<TLBotCommand>(br);

        }

        public override void SerializeBody(BinaryWriter bw)
        {
			bw.Write(Constructor);
            ObjectUtils.SerializeObject(Commands,bw);

        }
		public override void DeserializeResponse(BinaryReader br)
		{
			Response = BoolUtil.Deserialize(br);

		}
    }
}
