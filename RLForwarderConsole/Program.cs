using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.IO;
using System.Configuration;
using System.Timers;
using System.Threading;

namespace RLForwarderConsole
{
    internal class Program
    {
        private static Program instance;
        SerialPort mySerialPort = new SerialPort(ConfigurationManager.AppSettings["ComPort"]);
        Queue<string> linesQueue = new Queue<string>();
        List<string> currentBatch = new List<string>();
        

        static async Task Main(string[] args)
        {
            instance = new Program();
            instance.Run();
            await Task.Delay(-1); 
        }

        private void Run()
        {
            try
            {
                Logger.Log("Service started.");
                Console.WriteLine("Service started.");
                CheckConnectionWithDevice().Wait();
                mySerialPort.BaudRate = 9600;
                mySerialPort.Parity = Parity.None;
                mySerialPort.StopBits = StopBits.One;
                mySerialPort.DataBits = 8;
                mySerialPort.Handshake = Handshake.None;

                mySerialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);

                mySerialPort.Open();
                Console.WriteLine($"Serial {mySerialPort.PortName} open");
                Logger.Log($"Serial {mySerialPort.PortName} open");

                
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in OnStart: {ex.Message}");
                Console.WriteLine(ex.ToString());
            }
        }

        private static void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                SerialPort sp = (SerialPort)sender;
                string buffer = sp.ReadExisting();
                string[] lines = buffer.Split('\r');
                foreach (string line in lines)
                {
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }
                    instance.ProcessLine(line).Wait();
                    Console.WriteLine($"Line received: {line}");

                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in DataReceivedHandler: {ex.Message}");
                Console.WriteLine(ex.ToString());
            }
        }

        private async Task ProcessLine(string line)
        {
            string cleanedLine = CleanLine(line);
            linesQueue.Enqueue(cleanedLine);
            await ProcessQueue();
        }
        private async Task SendDataToApi()
        {
            try
            {
                var client = new HttpClient();
                client.BaseAddress = new Uri("http://" + ConfigurationManager.AppSettings["DeviceIP"].ToString());
                var ChannelID = ConfigurationManager.AppSettings["ChannelID"];
                var request = new HttpRequestMessage(HttpMethod.Put, $"/ISAPI/System/Video/inputs/channels/{ChannelID}/overlays");
                request.Headers.Add("Accept", "*/*");
                request.Headers.Add("User-Agent", "RLForwarder");
                request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
                request.Headers.Add("Connection", "keep-alive");
                string xmlPayload = CreateXmlPayload(currentBatch);
                request.Content = new StringContent(xmlPayload, Encoding.UTF8, "application/xml");
                var response = await client.SendWithDigestAuthAsync(request, HttpCompletionOption.ResponseContentRead, ConfigurationManager.AppSettings["ApiUsername"], ConfigurationManager.AppSettings["ApiPassword"]);
                if (response.IsSuccessStatusCode)
                {
                    currentBatch.RemoveAt(0);
                }
                await ProcessQueue();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in SendDataToApi: {ex.Message}");
                Console.WriteLine(ex.ToString());
            }
        }

        private async Task ProcessQueue()
        {
            while (linesQueue.Count > 0 && currentBatch.Count < 4)
            {
                currentBatch.Add(linesQueue.Dequeue());
            }
            if (currentBatch.Count == 4)
            {
                await SendDataToApi();
            }
        }
        private string CleanLine(string line)
        {
            Dictionary<string, string> translations = new Dictionary<string, string>
            {
                {"Forma płatn.", "Payment method:"},
                {"Razem:", "Total:"},
                {"Reszta:", "Change:"},
                {">> Paragon anulowany <<", ">> Receipt cancelled <<"},
                {"(STORNO)", "(VOID)"},
                {"(ZMIANA)", "(CHANGE)"},
                {">> Wznowienie pracy", ">> Work resumed"},
                {">> Karta klienta:", ">> Customer card:"},
                {">> Rabat na paragon:", ">> Receipt discount:"},
                {"Do zwrotu:", "Refund:"},
                {"Wyplata:", "Cash out:"},
                {"Wplata:", "Cash in:"},
                {"Zwrot do paragonu:", "Refund:"}

            };

            // Translate phrases
            foreach (var entry in translations)
            {
                line = line.Replace(entry.Key, entry.Value);
            }

            // Sprawdzenie, czy linia zawiera kwotę zakończoną na "zl."
            if ((line.Contains(" x ") && line.Contains("zl.")) 
                || (line.Contains("Razem: "))
                || (line.Contains("Reszta: "))
                )
            {
                // Znajdź pozycję, w której zaczyna się " x " i usuń "zl" oraz dodatkowe spacje wokół "x"
                int xPosition = line.LastIndexOf(" x ");
                if (xPosition != -1)
                {
                    int zlPosition = line.LastIndexOf("zl.", StringComparison.Ordinal);
                    if (zlPosition > xPosition)
                    {
                        // Usunięcie "zl" z końca linii
                        line = line.Substring(0, zlPosition) + line.Substring(zlPosition + 3);
                    }
                    // Usuwanie dodatkowych spacji wokół "x"
                    string beforeX = line.Substring(0, xPosition);
                    string afterX = line.Substring(xPosition + 3);

                    // Usuwamy zbędne spacje z końców ciągów przed i po "x"
                    beforeX = beforeX.TrimEnd();
                    afterX = afterX.TrimStart();

                    // Rekonstrukcja linii bez zbędnych spacji wokół "x"
                    line = beforeX + "x" + afterX;
                }
            }

            return line;
        }

        private string CreateXmlPayload(List<string> lines)
        {
            // Budowanie struktury XML z poszczególnych linii
            var xmlDoc = new XDocument(
                new XElement("VideoOverlay",
                    new XElement("normalizedScreenSize",
                        new XElement("normalizedScreenWidth", 704),
                        new XElement("normalizedScreenHeight", 576)),
                    new XElement("TextOverlayList",
                        lines.Select((line, index) =>
                            new XElement("TextOverlay",
                                new XElement("id", index + 1),
                                new XElement("enabled", true),
                                new XElement("positionX", 300),
                                new XElement("positionY", 500 - index * 30),  // Przesuwamy pozycję Y dla każdej kolejnej linii
                                new XElement("displayText", line.Length > 42 ? line.Substring(0, 42) : line),  // Obcinamy linię do 42 znaków, jeśli jest dłuższa
                                new XElement("isPersistentText", true)
                            )
                        )
                    )
                )
            );

            // Konwersja dokumentu XML do ciągu tekstowego
            return xmlDoc.ToString();
        }

        public static class Logger
        {
            private static string logDirectory = AppDomain.CurrentDomain.BaseDirectory;  // Ustaw ścieżkę katalogu na katalog bieżącej aplikacji
            private static string logFileName = "ServiceLog";
            private static string logFileExtension = ".txt";
            private static long maxLogFileSize = 100 * 1024 * 1024; // 100 MB

            private static string GetCurrentLogFilePath()
            {
                // Utwórz nazwę pliku z datą i numerem sekwencyjnym
                string date = DateTime.Now.ToString("yyyyMMdd");
                int index = 0;

                // Sprawdzaj, czy plik istnieje i czy jego rozmiar przekracza limit
                string path;
                do
                {
                    path = Path.Combine(logDirectory, $"{logFileName}_{date}_{index}{logFileExtension}");
                    index++;
                } while (File.Exists(path) && new FileInfo(path).Length > maxLogFileSize);

                return path;
            }

            public static void Log(string message)
            {
                try
                {
                    string logFilePath = GetCurrentLogFilePath();
                    using (StreamWriter sw = new StreamWriter(logFilePath, true))
                    {
                        sw.WriteLine($"{DateTime.Now}: {message}");
                    }
                }
                catch (Exception ex)
                {
                    throw;
                }
            }
        }

        async Task CheckConnectionWithDevice()
        {
            try
            {
                var client = new HttpClient();
                client.BaseAddress = new Uri("http://" + ConfigurationManager.AppSettings["DeviceIP"].ToString());
                var ChannelID = ConfigurationManager.AppSettings["ChannelID"];
                var request = new HttpRequestMessage(HttpMethod.Get, $"/ISAPI/System/Video/inputs/channels/{ChannelID}/overlays");
                request.Headers.Add("Accept", "*/*");
                request.Headers.Add("User-Agent", "RLForwarder");
                request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
                request.Headers.Add("Connection", "keep-alive");
                var response = await client.SendWithDigestAuthAsync(request, HttpCompletionOption.ResponseContentRead, ConfigurationManager.AppSettings["ApiUsername"], ConfigurationManager.AppSettings["ApiPassword"]);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"API connection error, device URL {client.BaseAddress.ToString()}");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in CheckConnectionWithDevice: {ex.Message}");
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
