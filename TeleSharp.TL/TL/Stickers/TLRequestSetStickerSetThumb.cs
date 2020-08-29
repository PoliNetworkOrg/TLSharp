using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeleSharp.TL;
namespace TeleSharp.TL.Stickers
{
	[TLObject(-1707717072)]
    public class TLRequestSetStickerSetThumb : TLMethod
    {
        public override int Constructor
        {
            get
            {
                return -1707717072;
            }
        }

                public TLAbsInputStickerSet Stickerset {get;set;}
        public TLAbsInputDocument Thumb {get;set;}
        public Messages.TLStickerSet Response{ get; set;}


		public void ComputeFlags()
		{
			
		}

        public override void DeserializeBody(BinaryReader br)
        {
            Stickerset = (TLAbsInputStickerSet)ObjectUtils.DeserializeObject(br);
Thumb = (TLAbsInputDocument)ObjectUtils.DeserializeObject(br);

        }

        public override void SerializeBody(BinaryWriter bw)
        {
			bw.Write(Constructor);
            ObjectUtils.SerializeObject(Stickerset,bw);
ObjectUtils.SerializeObject(Thumb,bw);

        }
		public override void DeserializeResponse(BinaryReader br)
		{
			Response = (Messages.TLStickerSet)ObjectUtils.DeserializeObject(br);

		}
    }
}
