using System.Collections.Generic;

namespace Akashic.Scripts.Model.Server.AI.FalAI
{
    public class FalAIGptImgResponse
    {
        public List<ImageResponse> images { get; set; }

        public class ImageResponse
        {
            public string url { get; set; }
            public string content_type { get; set; }
            public string file_name { get; set; }
            public long file_size { get; set; }
            public int? width { get; set; }
            public int? height { get; set; }
        }
    }
}