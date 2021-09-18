using System.IO;

namespace TeleSharp.TL.Messages
{
    [TLObject(333610782)]
    public class TLRequestEditChatAbout : TLMethod
    {
        public override int Constructor
        {
            get
            {
                return 333610782;
            }
        }

        public TLAbsInputPeer peer { get; set; }
        public string about { get; set; }
        public bool Response { get; set; }


        public void ComputeFlags()
        {

        }

        public override void DeserializeBody(BinaryReader br)
        {
            peer = (TLAbsInputPeer)ObjectUtils.DeserializeObject(br);
            about = StringUtil.Deserialize(br);

        }

        public override void SerializeBody(BinaryWriter bw)
        {
            ObjectUtils.SerializeObject(peer, bw);
            StringUtil.Serialize(about, bw);

        }
        public override void DeserializeResponse(BinaryReader br)
        {
            Response = BoolUtil.Deserialize(br);

        }
    }
}