using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Net;
using System.Resources;
using System.Xml.Linq;
using Data;

namespace WarshipGirlsFinalTool
{
    public static class helper
    {
        public static string GetNewUDID()
        {
            var md5 = MD5.Create();
            byte[] Bytes = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
            byte[] hash = md5.ComputeHash(Bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
        public static string GetMD5FromFile(string fileName)
        {
            var file = new FileStream(fileName, FileMode.Open);
            var md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(file);
            file.Close();
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
        public static long GetFileLength(string fileName)
        {
            var file = new FileInfo(fileName);
            return file.Length;
        }
    }
    internal static class Extension
    {
        public static long ToUTC(this DateTime vDate)
        {
            vDate = vDate.ToUniversalTime();
            DateTime dtZone = new DateTime(1970, 1, 1, 0, 0, 0);
            return (long)vDate.Subtract(dtZone).TotalMilliseconds;
        }
        public static string toHMS(this TimeSpan time)
        {
            return $"{(int) time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
        }

        public static string decompressZlibData(this byte[] data)
        {
            using (var memstream = new MemoryStream(data))
            {
                memstream.ReadByte();
                memstream.ReadByte();
                using (var dzip = new DeflateStream(memstream, CompressionMode.Decompress))
                {
                    using (var sr = new StreamReader(dzip))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
        }

        public static string decompressGZipData(this byte[] data)
        {
            using (var memstream = new MemoryStream(data))
            {
                using (var dzip = new GZipStream(memstream, CompressionMode.Decompress))
                {
                    using (var sr = new StreamReader(dzip))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
        }

        [DllImport("Shlwapi.dll", CharSet = CharSet.Auto)]
        private static extern long StrFormatByteSize(
        long fileSize
        , [MarshalAs(UnmanagedType.LPTStr)] StringBuilder buffer
        , int bufferSize);


        /// <summary>
        /// Converts a numeric value into a string that represents the number expressed as a size value in bytes, kilobytes, megabytes, or gigabytes, depending on the size.
        /// </summary>
        /// <param name="filesize">The numeric value to be converted.</param>
        /// <returns>the converted string</returns>
        public static string StrFormatByteSize(this long filesize)
        {
            StringBuilder sb = new StringBuilder(1024);
            StrFormatByteSize(filesize, sb, sb.Capacity);
            return sb.ToString();
        }

        public static string Base64Encode(this string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }
    }


    public class WebClientEx : WebClient
    {
        public readonly Dictionary<string, string> Cookies = new Dictionary<string, string>();

        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest r = base.GetWebRequest(address);
            var request = r as HttpWebRequest;
            if (request != null)
            {
                //request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
                if (BaseAddress != "")
                {
                    request.CookieContainer = new CookieContainer();
                    foreach (var cookie in Cookies)
                    {
                        request.CookieContainer.Add(
                            new Uri(BaseAddress), new Cookie(cookie.Key, cookie.Value)
                        );
                    }
                }
            }
            return r;
        }

        protected override WebResponse GetWebResponse(WebRequest request, IAsyncResult result)
        {
            WebResponse response = base.GetWebResponse(request, result);
            ReadCookies(response);
            return response;
        }

        protected override WebResponse GetWebResponse(WebRequest request)
        {
            WebResponse response = base.GetWebResponse(request);
            ReadCookies(response);
            return response;
        }

        private void ReadCookies(WebResponse r)
        {
            var response = r as HttpWebResponse;
            if (response != null)
            {
                foreach (Cookie cookie in response.Cookies)
                {
                    Cookies[cookie.Name] = cookie.Value;
                }
            }
        }
    }
}

//From https://www.codeproject.com/Tips/406235/A-Simple-PList-Parser-in-Csharp
namespace Data
{
    public class PList : Dictionary<string, dynamic>
    {
        public PList()
        {
        }

        public PList(string file)
        {
            Load(file);
        }

        public void Load(string file)
        {
            Clear();

            XDocument doc = XDocument.Load(file);
            XElement plist = doc.Element("plist");
            XElement dict = plist.Element("dict");

            var dictElements = dict.Elements();
            Parse(this, dictElements);
        }

        private void Parse(PList dict, IEnumerable<XElement> elements)
        {
            for (int i = 0; i < elements.Count(); i += 2)
            {
                XElement key = elements.ElementAt(i);
                XElement val = elements.ElementAt(i + 1);

                dict[key.Value] = ParseValue(val);
            }
        }

        private List<dynamic> ParseArray(IEnumerable<XElement> elements)
        {
            List<dynamic> list = new List<dynamic>();
            foreach (XElement e in elements)
            {
                dynamic one = ParseValue(e);
                list.Add(one);
            }

            return list;
        }

        private dynamic ParseValue(XElement val)
        {
            switch (val.Name.ToString())
            {
                case "string":
                    return val.Value;
                case "integer":
                    return int.Parse(val.Value);
                case "real":
                    return float.Parse(val.Value);
                case "true":
                    return true;
                case "false":
                    return false;
                case "dict":
                    PList plist = new PList();
                    Parse(plist, val.Elements());
                    return plist;
                case "array":
                    List<dynamic> list = ParseArray(val.Elements());
                    return list;
                default:
                    throw new ArgumentException("Unsupported");
            }
        }
    }
}