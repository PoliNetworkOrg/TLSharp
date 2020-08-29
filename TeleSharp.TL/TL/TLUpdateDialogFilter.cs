using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeleSharp.TL;
namespace TeleSharp.TL
{
	[TLObject(654302845)]
    public class TLUpdateDialogFilter : TLAbsUpdate
    {
        public override int Constructor
        {
            get
            {
                return 654302845;
            }
        }

             public int Flags {get;set;}
     public int Id {get;set;}
     public TLDialogFilter Filter {get;set;}


		public void ComputeFlags()
		{
			
		}

        public override void DeserializeBody(BinaryReader br)
        {
            Flags = br.ReadInt32();
Id = br.ReadInt32();
if ((Flags & 1) != 0)
Filter = (TLDialogFilter)ObjectUtils.DeserializeObject(br);
else
Filter = null;


        }

        public override void SerializeBody(BinaryWriter bw)
        {
			bw.Write(Constructor);
            bw.Write(Flags);
bw.Write(Id);
if ((Flags & 1) != 0)
ObjectUtils.SerializeObject(Filter,bw);

        }
    }
}
