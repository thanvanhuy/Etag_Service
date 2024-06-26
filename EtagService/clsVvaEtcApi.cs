using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;

namespace EtagService
{
    public class clsVvaEtcApi
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

        // cần thơ đổi sang tk cam thịnh để test tốc độ có phải do be
        public bool PostAPILogin()
        {
            string loginUrl = "https://vos.vetc.com.vn/auth/realms/vetc-utilities/protocol/openid-connect/token";
            //string username = "Cantho01";
            //string password = "cantho6868";
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
            WriteToFile("Get token succ " + DateTime.Now);
            return getTokenStatus;
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
                getTokenStatus = true;
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
                    WriteToFile("Fresh token succ " + DateTime.Now);
                }
                catch (Exception)
                {
                    Thread.Sleep(500);
                    PostAPILogin();
                    WriteToFile("Fresh token when err succ " + DateTime.Now);
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
        public bool PostAPI(string inputPlateNum, string plateType, ref string etag, ref string plate, ref string info)
        {
            string bienso = String.Concat(inputPlateNum, plateType);
            string url = "https://vos.vetc.com.vn/api/360/transaction-nearest?condition=" + bienso;
            string result = "";
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
                    List<data> myData = JsonConvert.DeserializeObject<List<data>>(result);
                    try
                    {
                        if (myData.Count > 0)
                        {
                            int bks = 0;
                            int inter = 0;
                            int succ = 0;
                            int row = 0;
                            List<string> MyList = new List<string>();
                            string etag1 = myData[0].etag_number ?? "";
                            if (etag1.Length < 0)
                            {
                                etag1 = myData[1].etag_number ?? "";
                                if (etag1.Length < 0)
                                {
                                    etag1 = myData[2].etag_number ?? "";
                                }
                            }
                            foreach (var dataetag in myData)
                            {
                                DateTime.TryParseExact(dataetag.transport_datetime, "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt);
                                string charge_status = dataetag.charge_status ?? "";
                                string etag_number = dataetag.etag_number ?? "";
                                string idtram = dataetag.toll_id ?? "";
                                string idlane = dataetag.lane_id ?? "";
                                idlane = idlane.Replace(".0", "");
                                string checkin_tid = dataetag.checkin_tid ?? "";
                                string checkout_tid = dataetag.checkout_tid ?? "";
                                if(dt.Year == 2023 && etag1 == etag_number)
                                {
                                    row++;
                                    if (checkin_tid == "BKS" || checkin_tid == "BSX")
                                    {
                                        bks++;
                                    }
                                    if (checkout_tid == "BKS" || checkout_tid == "BSX")
                                    {
                                        bks++;
                                    }
                                    switch (idtram)
                                    {
                                        case "417":
                                        case "413":
                                            {
                                                if ((charge_status.StartsWith("IMME") || charge_status.StartsWith("INTER")))
                                                {
                                                    if (idlane.Equals("3") || (idlane.Equals("4")))
                                                    {
                                                        inter++;
                                                        MyList.Add("IMME");
                                                    }
                                                }
                                                if (charge_status.Equals("SUCC"))
                                                {
                                                    if (idlane.Equals("3") || (idlane.Equals("4")))
                                                    {
                                                        succ++;
                                                        MyList.Add("SUCC");
                                                    }
                                                }
                                                break;
                                            }
                                        case "418":
                                        case "424":
                                        case "412":
                                        case "423":
                                        case "960":
                                            {
                                                if ((charge_status.StartsWith("IMME") || charge_status.StartsWith("INTER")))
                                                {
                                                    inter++;
                                                    MyList.Add("IMME");
                                                }
                                                if (charge_status.Equals("SUCC"))
                                                {
                                                    succ++;
                                                    MyList.Add("SUCC");
                                                }
                                                break;
                                            }
                                        case "1181":
                                            {
                                                if ((charge_status.StartsWith("IMME") || charge_status.StartsWith("INTER")))
                                                {
                                                    if (idlane.Equals("3") || (idlane.Equals("5")))
                                                    {
                                                        inter++;
                                                        MyList.Add("IMME");
                                                    }
                                                }
                                                if (charge_status.Equals("SUCC"))
                                                {
                                                    if (idlane.Equals("3") || (idlane.Equals("5")))
                                                    {
                                                        succ++;
                                                        MyList.Add("SUCC");
                                                    }
                                                }
                                                break;
                                            }
                                        case "1180":
                                            {
                                                if ((charge_status.StartsWith("IMME") || charge_status.StartsWith("INTER")))
                                                {
                                                    if (idlane.Equals("4") || (idlane.Equals("5")))
                                                    {
                                                        inter++;
                                                        MyList.Add("IMME");
                                                    }
                                                }
                                                if (charge_status.Equals("SUCC"))
                                                {
                                                    if (idlane.Equals("4") || (idlane.Equals("5")))
                                                    {
                                                        succ++;
                                                        MyList.Add("SUCC");
                                                    }
                                                }
                                                break;
                                            }
                                        default:
                                            break;
                                    }
                                }
                            }
                                
                            info = "0";
                            if (succ > 0 && inter == 0)
                            {
                                if (bks > 30 || bks >= row || (row - bks < 5 && row > 10))
                                {
                                    info = "999";
                                }
                                else
                                    info = "0";
                            }
                            if (succ == 0 && inter == 0)
                            {
                                if (row > 20)
                                {
                                    if (row == 50)
                                    {
                                        if (bks > 35)
                                        {
                                            info = "999";
                                        }
                                    }
                                    else
                                    {
                                        if (bks > 15)
                                        {
                                            info = "999";
                                        }
                                    }
                                }
                                else
                                {
                                    if (bks > 8)
                                    {
                                        info = "999";
                                    }
                                }

                            }
                            if (succ == 0 && inter > 0)
                            {
                                info = "999";
                            }
                            if (succ > 0 && inter > 0)
                            {
                                if (row > 20)
                                {
                                    if (row < 35)
                                    {
                                        if (bks > 8 || inter > 10)
                                        {
                                            info = "999";
                                        }
                                        else
                                        {
                                            if (MyList.Count > 2)
                                            {
                                                if (MyList[0] == "IMME" && MyList[1] == "IMME")
                                                {
                                                    info = "999";
                                                }
                                                else
                                                {

                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (bks > 10 || inter>15)
                                        {
                                            info = "999";
                                        }
                                        else
                                        {
                                            if (MyList.Count > 2)
                                            {
                                                if (MyList[0] == "IMME" && MyList[1] == "IMME")
                                                {
                                                    info = "999";
                                                }
                                            }
                                        }
                                    }

                                }
                                else
                                {
                                    if (bks > 6 || inter > 8)
                                    {
                                        info = "999";
                                    }
                                    else
                                    {
                                        if (MyList.Count > 2)
                                        {
                                            if (MyList[0] == "IMME" && MyList[1] == "IMME")
                                            {
                                                info = "999";
                                            }
                                            else
                                            {
                                                if (row < 10 && bks > 3)
                                                {
                                                    info = "999";
                                                }
                                            } 
                                        }
                                    }
                                }
                            }
                            if (myData[0].etag_number is null)
                            {
                                etag = "";
                                plate = "";
                            }
                            else
                            {
                                etag = myData[0].etag_number;
                                if (myData[0].plate is null)
                                {
                                    plate = String.Concat(inputPlateNum, plateType);
                                }
                                else
                                {
                                    plate = myData[0].plate;
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
                catch (WebException)
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
    public class data
    {
        public string transport_trans_id { get; set; }
        public string transport_datetime { get; set; }
        public string transport_datetime_order { get; set; }
        public string lane_id { get; set; }
        public string checkout_datetime { get; set; }
        public string checkout_toll_id { get; set; }
        public string checkout_toll_name { get; set; }
        public string checkout_lane_id { get; set; }
        public string charge_status { get; set; }
        public string pass_reason_id { get; set; }
        public string checkin_tid { get; set; }
        public string checkout_tid { get; set; }
        public string price_type { get; set; }
        public string price_amount { get; set; }
        public string boo { get; set; }
        public string toll_type { get; set; }
        public string etag_number { get; set; }
        public string toll_id { get; set; }
        public string toll_name { get; set; }
        public string plate { get; set; }
    }
}
