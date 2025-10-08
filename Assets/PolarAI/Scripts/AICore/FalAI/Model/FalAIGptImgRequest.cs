namespace Akashic.Scripts.Model.Server.AI.FalAI
{
    public class FalAIGptImgRequest
    {
        public string prompt { get; set; }
        public string[] image_urls { get; set; }
        public string image_size { get; set; }
        public int num_images { get; set; }
        public string quality { get; set; } // auto, low, medium, high
        public string background { get; set; } // transparent, opaque, auto

        public FalAIGptImgRequest()
        {
            prompt = string.Empty;
            image_urls = new string[] { };
            image_size = "1024x1024";
            num_images = 1;
            quality = "auto";
            background = "transparent";
        }
    }
}