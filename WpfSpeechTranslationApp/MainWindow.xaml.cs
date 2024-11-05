
using System.Windows;
using System.Windows.Controls;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Wave;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using System.IO;

namespace WpfSpeechTranslationApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string currentAudioFilePath;
        private WaveInEvent waveIn;
        private WaveFileWriter writer;
        public MainWindow()
        {
            InitializeComponent();
        }

        // кнопка для начала записи
        private void BtnStartRecording_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string outputFilePath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "RecordedAudio.wav");
                currentAudioFilePath = outputFilePath;

                waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(16000, 1)
                };
                writer = new WaveFileWriter(outputFilePath, waveIn.WaveFormat);

                waveIn.DataAvailable += (s, args) =>
                {
                    writer.Write(args.Buffer, 0, args.BytesRecorded);
                };

                waveIn.RecordingStopped += (s, args) =>
                {
                    writer.Dispose();
                    writer = null;
                    waveIn.Dispose();
                    waveIn = null;
                };

                waveIn.StartRecording();
                MessageBox.Show("Запись началась. Нажмите 'Остановить запись', чтобы закончить.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка записи: " + ex.Message);
            }
        }


        // событие для кнопки, которая начинает процесс распознавания и перевода     
        private async void BtnRecognize_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(currentAudioFilePath) && File.Exists(currentAudioFilePath))
            {
                try
                {
                    var speechConfig = SpeechConfig.FromSubscription("0b7469c0fe394248bda59bb140eb4cde", "francecentral");
                    //speechConfig.SpeechRecognitionLanguage = "ru-RU";  
                    // устанавливаем язык для распознавания из ComboBox
                    speechConfig.SpeechRecognitionLanguage = ((ComboBoxItem)cmbLanguage.SelectedItem).Content.ToString();



                    using (var audioConfig = AudioConfig.FromWavFileInput(currentAudioFilePath))
                    using (var recognizer = new SpeechRecognizer(speechConfig, audioConfig))
                    {
                        var result = await recognizer.RecognizeOnceAsync();
                        if (result == null || string.IsNullOrEmpty(result.Text))
                        {
                            MessageBox.Show("Ошибка: не удалось распознать текст.");
                            txtTranscription.Text = "Распознавание не удалось.";
                            return;
                        }
                        txtTranscription.Text = "Транскрипция: " + result.Text;
                        // извлекаем исходный язык для перевода из ComboBox
                        string sourceLanguage = ((ComboBoxItem)cmbLanguage.SelectedItem).Content.ToString().Split('-')[0];

                        // извлекаем целевой язык из ComboBox
                        string targetLanguage = ((ComboBoxItem)cmbTargetLanguage.SelectedItem).Content.ToString();

                        // выполняем перевод на выбранный язык
                        string translation = await TranslateText(result.Text, sourceLanguage, targetLanguage);
                        if (translation == null)
                        {
                            txtTranslation.Text = "Ошибка перевода.";
                            MessageBox.Show("Ошибка перевода: получен пустой результат.");
                        }
                        else
                        {
                            txtTranslation.Text = "Перевод: " + translation;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка распознавания: " + ex.Message);
                }
            }
            else
            {
                MessageBox.Show("Файл для распознавания не найден. Сначала запишите аудио.");
            }
        }


        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (waveIn != null)
            {
                waveIn.StopRecording();
                MessageBox.Show("Запись остановлена и сохранена.");
            }
        }

        private async Task<string> TranslateText(string text, string fromLanguage, string toLanguage)
        {
            string endpoint = "https://api.cognitive.microsofttranslator.com";
            string route = $"/translate?api-version=3.0&from={fromLanguage}&to={toLanguage}";
            string subscriptionKey = "525ce21b48dd4472b1eedd0fda087c58";
            string region = "francecentral"; 

            try
            {
                using (HttpClient client = new HttpClient())
                using (HttpRequestMessage request = new HttpRequestMessage())
                {
                    object[] body = new object[] { new { Text = text } };
                    string requestBody = JsonConvert.SerializeObject(body);

                    request.Method = HttpMethod.Post;
                    request.RequestUri = new Uri(endpoint + route);
                    request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                    request.Headers.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
                    request.Headers.Add("Ocp-Apim-Subscription-Region", region);

                   // MessageBox.Show($"Перевод с {fromLanguage} на {toLanguage}: {text}");


                    HttpResponseMessage response = await client.SendAsync(request);
                    string result = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        // десериализуем ответ как список (массив JSON)
                        var translationResults = JsonConvert.DeserializeObject<List<TranslationApiResponse>>(result);
                        return translationResults.FirstOrDefault()?.Translations.FirstOrDefault()?.Text ?? "Translation failed";
                    }
                    else
                    {
                        MessageBox.Show("Ошибка при обращении к API перевода: " + response.ReasonPhrase);
                        return "Translation failed";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при переводе: " + ex.Message);
                return "Translation failed";
            }
        }


        public class TranslationApiResponse
        {
            public List<TranslationResult> Translations { get; set; }
        }

        public class TranslationResult
        {
            public string Text { get; set; }
        }

    }
}
