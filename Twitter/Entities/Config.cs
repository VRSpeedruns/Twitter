using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Twitter.Entities
{
    [Serializable]
    class Config
    {
        public string consumerKey;
        public string consumerSecret;
        public string accessToken;
        public string accessTokenSecret;

        public bool HasEmpty()
        {
            return consumerKey == ""
                || consumerSecret == ""
                || accessToken == ""
                || accessTokenSecret == "";
        }
    }
}
