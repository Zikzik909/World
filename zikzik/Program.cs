using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Zikzik
{
    class Program
    {
        private static readonly string token = "y0__xCdxbvDCBjUwzsgw_qGkBUwicWc-AfJ2tYeX4_nZruHkWr40ByOSDJr6A";

        static async Task Main(string[] args)
        {
            Console.WriteLine("Меню:\n1 - Просмотр файлов\n2 - Загрузка локального файла на Яндекс.Диск\n3 - Выгрузка (скачивание) файла с Яндекс.Диска\n0 - Выход");

            bool exit = false;
            while (!exit)
            {
                Console.Write("\nВыберите действие (0-3): ");
                string choice = Console.ReadLine()?.Trim();

                switch (choice)
                {
                    case "1":
                        await ListFiles();
                        break;
                    case "2":
                        await UploadFile();
                        break;
                    case "3":
                        await DownloadFile();
                        break;
                    case "0":
                        exit = true;
                        break;
                    default:
                        Console.WriteLine("Неверный ввод, попробуйте снова.");
                        break;
                }
            }

            Console.WriteLine("Выход...");
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"OAuth {token}");
            return client;
        }

        private static async Task ListFiles()
        {
            try
            {
                using (var client = CreateClient())
                {
                    string url = "https://cloud-api.yandex.net/v1/disk/resources/files?limit=100";
                    var resp = await client.GetAsync(url);
                    if (!resp.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Ошибка получения списка файлов: " + resp.ReasonPhrase);
                        return;
                    }

                    string json = await resp.Content.ReadAsStringAsync();
                    var items = JObject.Parse(json)["items"];
                    Console.WriteLine("Файлы на Яндекс.Диске:");
                    if (items == null)
                    {
                        Console.WriteLine("(нет объектов)");
                        return;
                    }
                    foreach (var it in items)
                    {
                        Console.WriteLine(it["name"] + "  |  path: " + it["path"]);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка: " + ex.Message);
            }
        }

        private static async Task UploadFile()
        {
            try
            {
                Console.Write("Введите полный локальный путь к файлу для загрузки (или нажмите Enter чтобы выбрать из текущей папки): ");
                string localPath = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(localPath))
                {
                    var files = Directory.GetFiles(Directory.GetCurrentDirectory());
                    if (files.Length == 0)
                    {
                        Console.WriteLine("В текущей папке нет файлов.");
                        return;
                    }

                    Console.WriteLine("Файлы в текущей папке:");
                    for (int i = 0; i < files.Length; i++)
                    {
                        Console.WriteLine($"{i + 1}. {Path.GetFileName(files[i])}");
                    }

                    Console.Write($"Выберите номер файла (Enter = 1): ");
                    string choice = Console.ReadLine()?.Trim();
                    int index = 0;
                    if (!string.IsNullOrEmpty(choice) && int.TryParse(choice, out int parsed))
                    {
                        if (parsed >= 1 && parsed <= files.Length) index = parsed - 1;
                        else
                        {
                            Console.WriteLine("Неверный номер. Берётся первый файл.");
                            index = 0;
                        }
                    }
                    localPath = files[index];
                }

                if (string.IsNullOrEmpty(localPath) || !File.Exists(localPath))
                {
                    Console.WriteLine("Файл не найден: " + localPath);
                    return;
                }

                string diskPath = "/" + Path.GetFileName(localPath);
                Console.WriteLine($"Файл будет загружен на Яндекс.Диск по пути: {diskPath}");

                string encodedPath = Uri.EscapeDataString(diskPath);
                string getHrefUrl = $"https://cloud-api.yandex.net/v1/disk/resources/upload?path={encodedPath}&overwrite=true";

                using (var client = CreateClient())
                {
                    var resp = await client.GetAsync(getHrefUrl);
                    if (!resp.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Не удалось получить ссылку на загрузку: " + resp.ReasonPhrase);
                        string txt = await resp.Content.ReadAsStringAsync();
                        Console.WriteLine(txt);
                        return;
                    }

                    var json = JObject.Parse(await resp.Content.ReadAsStringAsync());
                    string href = (string)json["href"];
                    if (string.IsNullOrEmpty(href))
                    {
                        Console.WriteLine("Ссылка на загрузку не получена.");
                        return;
                    }

                    using (var fs = File.OpenRead(localPath)) using (var putClient = new HttpClient())
                    using (var content = new StreamContent(fs))
                    {
                        var putResp = await putClient.PutAsync(href, content);
                        if (putResp.IsSuccessStatusCode)
                        {
                            Console.WriteLine("Файл успешно загружен на Яндекс.Диск.");
                        }
                        else
                        {
                            Console.WriteLine("Ошибка при загрузке файла: " + putResp.ReasonPhrase);
                            Console.WriteLine(await putResp.Content.ReadAsStringAsync());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка: " + ex.Message);
            }
        }

        private static async Task DownloadFile()
        {
            try
            {
                Console.Write("Введите путь файла на Яндекс.Диске (например, /test.txt или myfolder/test.txt): ");
                string diskPath = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(diskPath))
                {
                    Console.WriteLine("Неверный путь на диске.");
                    return;
                }

                Console.Write("Введите локальный путь для сохранения (полный путь или папку, если хотите сохранить под тем же именем): ");
                string localDest = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(localDest))
                {
                    Console.WriteLine("Неверный локальный путь.");
                    return;
                }

                bool localIsDirectory = Directory.Exists(localDest) || localDest.EndsWith(Path.DirectorySeparatorChar.ToString()) || localDest.EndsWith("/");
                string fileName = Path.GetFileName(diskPath.TrimEnd('/', '\\'));
                string finalLocalPath = localIsDirectory ? Path.Combine(localDest, fileName) : localDest;

                string encodedPath = Uri.EscapeDataString(diskPath);
                string getHrefUrl = $"https://cloud-api.yandex.net/v1/disk/resources/download?path={encodedPath}";

                using (var client = CreateClient())
                {
                    var resp = await client.GetAsync(getHrefUrl);
                    if (!resp.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Не удалось получить ссылку на скачивание: " + resp.ReasonPhrase);
                        Console.WriteLine(await resp.Content.ReadAsStringAsync());
                        return;
                    }

                    var json = JObject.Parse(await resp.Content.ReadAsStringAsync());
                    string href = (string)json["href"];
                    if (string.IsNullOrEmpty(href))
                    {
                        Console.WriteLine("Ссылка на скачивание не получена.");
                        return;
                    }

                    using (var downloadClient = new HttpClient())
                    using (var response = await downloadClient.GetAsync(href, HttpCompletionOption.ResponseHeadersRead))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            Console.WriteLine("Ошибка при скачивании файла: " + response.ReasonPhrase);
                            return;
                        }

                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fs = File.Create(finalLocalPath))
                        {
                            await stream.CopyToAsync(fs);
                        }

                        Console.WriteLine($"Файл сохранён: {finalLocalPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка: " + ex.Message);
            }
        }
    }
}

