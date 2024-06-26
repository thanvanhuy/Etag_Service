using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace vvaEtcApi
{
    public  class clsVvaEtcApi
    {
        private static string client_id = "vetc-utilities";
        private string client_secret = "f54f5dd2-3e08-4590-9f83-89376683cb21";
        private clsToken data_Token;
        private object syncObject = new object();
        public bool getTokenStatus { get; set; }
        class clsToken
        {
            public string access_token { get; set; }
            public int expires_in { get; set; }
            public int refresh_expires_in { get; set; }
            public string refresh_token { get; set; }
            public string token_type { get; set; }
            public string session_state { get; set; }
            public string scope { get; set; }
        }
        private static HttpClient client = new HttpClient();
        private CookieCollection cookies = new CookieCollection();

      
        public  bool  PostAPILogin()
        {
            string loginUrl = "https://vos.vetc.com.vn/auth/realms/vetc-utilities/protocol/openid-connect/token";
            string username = "camthinh01";
            string password = "vetc12345";
            
            Thread th = new Thread(async () =>
            {
                await getToKen(loginUrl, username, password);
            });
            th.Priority = ThreadPriority.Highest;
            th.IsBackground = true;
            th.Start();
            Thread.Sleep(1500);
            WriteToFile("Get token succ" + DateTime.Now);
            return getTokenStatus ;
        }
        private async Task getToKen(string url, string username, string password)
        {
            var loginContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password),
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("client_id", client_id),
                new KeyValuePair<string, string>("client_secret", client_secret),
        });

            var loginResponse = await client.PostAsync(url, loginContent);
            var accessToken = loginResponse.Content.ReadAsStringAsync().Result;

            clsToken token = JsonConvert.DeserializeObject<clsToken>(accessToken);
            if (token == null)
            {
                getTokenStatus = false;
            }             
            else
            {
                data_Token = token;
                getTokenStatus = true ;
            }    
              
        }
        private async Task<string> Search(string url)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(data_Token.token_type, data_Token.access_token);
            var response = await client.GetAsync(url);
            if (response.StatusCode == HttpStatusCode.OK) //Thành công
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return responseContent;
            }
            else if (response.StatusCode == HttpStatusCode.Unauthorized) 
            {
                Thread.Sleep(500);
                try
                {
                    PostAPILogin();
                    WriteToFile("Fresh token succ" + DateTime.Now);
                }
                catch (Exception)
                {
                    Thread.Sleep(500);
                    PostAPILogin();
                    WriteToFile("Fresh token when err succ" + DateTime.Now);
                }
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(data_Token.token_type, data_Token.access_token);
                response = await client.GetAsync(url);
                var responseContent = await response.Content.ReadAsStringAsync();
                return responseContent;
            }
            else
            {
                return "";
            }    
        }
        public  async void xuLy(string inputPlateNum, string plateType)
        {

            string bienso = String.Concat(inputPlateNum, plateType);
            string url = "https://vos.vetc.com.vn/api/360/transaction-nearest?condition=" + bienso;
            string searchResult = await  Search (url);

        }
        public bool  PostAPI(string inputPlateNum, string plateType, ref string etag, ref string plate, ref string info)
        {
            string bienso = String.Concat(inputPlateNum,plateType );
            string url = "https://vos.vetc.com.vn/api/360/transaction-nearest?condition=" + bienso;
            string result  = "";

            Thread th = new Thread(async () =>
            {
                string searchResult = await Search(url);
                result = searchResult;
            });
            th.Priority = ThreadPriority.Highest;
            th.IsBackground = true;
            th.Start();

            for (int i = 0; i < 120; i++)
            {
                Thread.Sleep(15);
                if (result.Length > 0)
                {
                    break;
                }
            }
            if (result.Length > 0)
            {
                try
                {
                    data[] myData = JsonConvert.DeserializeObject<data[]>(result);
                    try
                    {
                        info = "0";
                        if (myData.Length > 0)
                        {
                            int bks = 0;
                            int counter = 0;
                            int viTriSucc = -1;
                            int viTriOffline = -1;
                            int soLanSucc = 0;
                            for (int i = 0; i < myData.Length - 1; i++)
                            {
                                if (!(myData[i].CHARGE_STATUS is null))
                                {
                                    if (myData[i].TOLL_ID.CompareTo("413") == 0)
                                    {
                                        if (myData[i].CHARGE_STATUS.CompareTo("INTERMEDIATE") == 0 || myData[i].CHARGE_STATUS.CompareTo("IMMEDIATE") == 0)
                                        {
                                            if (!(myData[i].LANE_ID is null))
                                            {
                                                if (myData[i].LANE_ID.Equals("3") || myData[i].LANE_ID.Equals("4"))
                                                {
                                                    counter += 1;
                                                    if (counter >= 3)
                                                    {
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                        else if (myData[i].CHARGE_STATUS.Equals("SUCC"))
                                        {
                                            if (!(myData[i].LANE_ID is null))
                                            {
                                                if (myData[i].LANE_ID.Equals("3") || myData[i].LANE_ID.Equals("4"))
                                                {
                                                    soLanSucc += 1;
                                                    if (viTriSucc < 0)
                                                    {
                                                        viTriSucc = i;
                                                    }
                                                }
                                            }

                                        }
                                    }

                                }
                                if (!(myData[i].CHECKIN_TID is null))
                                {
                                    if (myData[i].CHECKIN_TID.Equals("BKS"))
                                    {
                                        if ((myData[i].CHECKOUT_TID is null))
                                        {
                                            bks += 1;
                                            if (bks >= 3)
                                            {
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            try
                                            {
                                                string kq = myData[i].CHECKOUT_TID;
                                                kq = kq.Replace(" ", "");
                                                if (kq.Length <= 0)
                                                {
                                                    bks += 1;
                                                    if (bks >= 3)
                                                    {
                                                        break;
                                                    }
                                                }
                                            }
                                            catch (Exception)
                                            {

                                            }
                                        }


                                    }
                                }

                            }
                            if (counter >= 2)
                            {
                                info = "999";

                            }
                            else if (bks >= 3)
                            {
                                info = "999";
                            }

                            if (counter <= 0 && soLanSucc > 4)
                            {
                                info = "0";
                            }


                            if (myData[0].ETAG_NUMBER is null)
                            {
                                etag = "";
                                plate = "";
                            }
                            else
                            {
                                etag = myData[0].ETAG_NUMBER;
                                if (myData[0].PLATE is null)
                                {
                                    plate = String.Concat(inputPlateNum, plateType);
                                }
                                else
                                {
                                    plate = myData[0].PLATE;
                                }
                            }

                            return true;

                        }
                        else
                        {
                            etag = "";
                            plate = "";
                            info = "";
                        }
                    }
                    catch (Exception)
                    {
                        etag = "";
                        plate = "";
                        info = "";
                    }
                }
                catch (WebException ex)
                {

                }
            }
            return false;
        }
        public void WriteToFile(string Message)
        {
            new Thread(() =>
            {
                lock (syncObject)
                {
                    try
                    {
                        string path = AppDomain.CurrentDomain.BaseDirectory + "\\Logs";
                        if (!Directory.Exists(path))
                        {
                            Directory.CreateDirectory(path);
                        }
                        string filepath = AppDomain.CurrentDomain.BaseDirectory + "\\Logs\\ServiceLog_" + DateTime.Now.ToString("yyMMdd") + ".txt";
                        if (!File.Exists(filepath))
                        {
                            using (StreamWriter sw = File.CreateText(filepath))
                            {
                                sw.WriteLine(Message);
                            }
                        }
                        else
                        {
                            using (StreamWriter sw = File.AppendText(filepath))
                            {
                                sw.WriteLine(Message);
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }).Start();

        }
    }
   
    public class D
    {
        public int wId { get; set; }
        public string content { get; set; }
        public string data { get; set; }
    }
    public class Root
    {
        public bool e { get; set; }
        public string m { get; set; }
        public D d { get; set; }
    }
    public class data
    {
        public string LANE_ID { get; set; }
        public string CHARGE_STATUS { get; set; }
        public string TOLL_ID { get; set; }
        public string ETAG_NUMBER { get; set; }
        public string PLATE { get; set; }
        public string CHECKOUT_TID { get; set; }
        public string CHECKIN_TID { get; set; }
    }

}
