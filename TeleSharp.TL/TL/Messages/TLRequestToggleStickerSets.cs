using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeleSharp.TL;
namespace TeleSharp.TL.Messages
{
	[TLObject(-1257951254)]
    public class TLRequestToggleStickerSets : TLMethod
    {
        public override int Constructor
        {
            get
            {
                return -1257951254;
            }
        }

                public int Flags {get;set;}
        public bool Uninstall {get;set;}
        public bool Archive {get;set;}
        public bool Unarchive {get;set;}
        public TLVector<TLAbsInputStickerSet> Stickersets {get;set;}
        public bool Response{ get; set;}


		public void ComputeFlags()
		{
			
		}

        public override void DeserializeBody(BinaryReader br)
        {
            Flags = br.ReadInt32();
Uninstall = (Flags & 1) != 0;
Archive = (Flags & 2) != 0;
Unarchive = (Flags & 4) != 0;
Stickersets = (TLVector<TLAbsInputStickerSet>)ObjectUtils.DeserializeVector<TLAbsInputStickerSet>(br);

        }

        public override void SerializeBody(BinaryWriter bw)
        {
			bw.Write(Constructor);
            bw.Write(Flags);



ObjectUtils.SerializeObject(Stickersets,bw);

        }
		public override void DeserializeResponse(BinaryReader br)
		{
			Response = BoolUtil.Deserialize(br);

		}
    }
}
