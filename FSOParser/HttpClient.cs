// Copyright (c) 2024 Robert Bosch GmbH
// All rights reserved.
//
// This source code is licensed under the MIT license found in the
// LICENSE file in the root directory of this source tree.

// The orginal code is by Ali Kücükavci from the RevitToRDFConverter https://github.com/Semantic-HVAC-Tool/Parser
// Under the MIT license

using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace FSOParser
{


    public class HttpClientHelper
    {
        private static HttpClient client = new HttpClient();

        public static async Task<string> POSTDataAsync(string data)
        {

            //var data1 = data.ToString();
            //var data2 = new StringContent(JsonConvert.SerializeObject(data1), Encoding.UTF8, "text/turtle");
            var data2 = new StringContent(data, null, "text/turtle");
            var url = "http://localhost:7200/repositories/Renningen/rdf-graphs/service?default"; //User needs to specify the repository's URL
            HttpResponseMessage response = await client.PostAsync(url, data2);
            string result = response.Content.ReadAsStringAsync().Result;

            return result;
        }
    }


}

