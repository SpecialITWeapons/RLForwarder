using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Configuration;
using System.IO;

namespace RLForwarder
{
    public partial class Service1 : ServiceBase
    {
        SerialPort mySerialPort = new SerialPort("COM9");
        Queue<string> linesQueue = new Queue<string>();
        List<string> currentBatch = new List<string>();
        public void Start() => OnStart(Array.Empty<string>());
        public Service1()
        {
            InitializeComponent();
        }
        
        async void ProcessLine(string line)
        {
            string cleanedLine = CleanCurrencySuffix(line);
            linesQueue.Enqueue(cleanedLine);
            await  ProcessQueue();
        }

        async Task SendDataToApi()
        {
            try
            {
                var client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Put, ConfigurationManager.AppSettings["ApiUrl"]);
                request.Headers.Authorization = new AuthenticationHeaderValue("Digest", "admin:Haslo123");

                string xmlPayload = CreateXmlPayload(currentBatch);
                request.Content = new StringContent(xmlPayload, Encoding.UTF8, "application/xml");

                var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    // Usuń pierwszą linię z batcha
                    currentBatch.RemoveAt(0);
                }

                // Spróbuj przetworzyć kolejne linie, jeśli są dostępne
                await ProcessQueue();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in SendDataToApi: {ex.Message}");
            }
        }

        async Task ProcessQueue()
        {
            // Dopóki w kolejce są linie do przetworzenia
            while (linesQueue.Count > 0 && currentBatch.Count < 4)
            {
                // Dodaj nową linię do aktualnego batcha
                currentBatch.Add(linesQueue.Dequeue());
            }

            // Jeśli zebrano 4 linie, wyślij je
            if (currentBatch.Count == 4)
            {
                await SendDataToApi();
            }
        }
        string CleanCurrencySuffix(string line)
        {
            // Sprawdzenie, czy linia zawiera kwotę zakończoną na "zl."
            if (line.Contains(" x ") && line.EndsWith("zl."))
            {
                // Znajdź pozycję, w której zaczyna się " x " i usuń "zl" oraz dodatkowe spacje wokół "x"
                int xPosition = line.LastIndexOf(" x ");
                if (xPosition != -1)
                {
                    int zlPosition = line.LastIndexOf("zl.", StringComparison.Ordinal);
                    if (zlPosition > xPosition)
                    {
                        // Usunięcie "zl" z końca linii
                        line = line.Substring(0, zlPosition) + line.Substring(zlPosition + 2);
                    }
                    // Usuwanie dodatkowych spacji wokół "x"
                    string beforeX = line.Substring(0, xPosition);
                    string afterX = line.Substring(xPosition + 3);

                    // Usuwamy zbędne spacje z końców ciągów przed i po "x"
                    beforeX = beforeX.TrimEnd();
                    afterX = afterX.TrimStart();

                    // Rekonstrukcja linii bez zbędnych spacji wokół "x"
                    line = beforeX + " x " + afterX;
                }
            }
            return line;
        }

        string CreateXmlPayload(List<string> lines)
        {
            // Budowanie struktury XML z poszczególnych linii
            var xmlDoc = new XDocument(
                new XElement("VideoOverlay", new XAttribute("version", "2.0"), new XAttribute(XNamespace.Xmlns + "xmlns", "http://www.hikvision.com/ver20/XMLSchema"),
                    new XElement("normalizedScreenSize",
                        new XElement("normalizedScreenWidth", 704),
                        new XElement("normalizedScreenHeight", 576)),
                    new XElement("TextOverlayList",
                        lines.Select((line, index) =>
                            new XElement("TextOverlay",
                                new XElement("id", index + 1),
                                new XElement("enabled", true),
                                new XElement("positionX", 300),
                                new XElement("positionY", 500 - index * 40),  // Przesuwamy pozycję Y dla każdej kolejnej linii
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

        protected override void OnStart(string[] args)
        {
            try
            {
                mySerialPort.BaudRate = 9600;
                mySerialPort.Parity = Parity.None;
                mySerialPort.StopBits = StopBits.One;
                mySerialPort.DataBits = 8;
                mySerialPort.Handshake = Handshake.None;

                mySerialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);

                mySerialPort.Open();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in OnStart: {ex.Message}");
            }


        }
        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                SerialPort sp = (SerialPort)sender;
                string indata = sp.ReadLine();
                ProcessLine(indata);
                Logger.Log("Data processed successfully.");
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in DataReceivedHandler: {ex.Message}");
            }
        }
        protected override void OnStop()
        {
            mySerialPort.Close();
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
    }
}
