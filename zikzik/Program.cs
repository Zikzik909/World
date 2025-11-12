using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Zikzik
{
    class Program
    {
        private static bool isFirstKeyPress = true;

        static async Task Main(string[] args)
        {
            Console.WriteLine("Нажмите любую клавишу для получения файлов с Яндекс Диска...");

            Console.ReadKey();

            await DisplayFiles(); 

            Console.WriteLine("\nНажмите любую клавишу для выхода...");
            Console.ReadKey();
        }

        private static async Task DisplayFiles()
        {          
            if (isFirstKeyPress)
            {
                isFirstKeyPress = false;

                string token = "y0__xCdxbvDCBjUwzsgw_qGkBUwicWc-AfJ2tYeX4_nZruHkWr40ByOSDJr6A"; 
                string url = "https://cloud-api.yandex.net/v1/disk/resources/files";

                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"OAuth {token}");

                    var response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var jsonResponse = await response.Content.ReadAsStringAsync();
                        var files = JObject.Parse(jsonResponse)["items"];

                        Console.WriteLine("Список ваших файлов на Яндекс Диске:");
                        foreach (var file in files)
                        {
                            Console.WriteLine(file["name"]);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Ошибка получения файлов: " + response.ReasonPhrase);
                    }
                }
            }
        }
    }
}

