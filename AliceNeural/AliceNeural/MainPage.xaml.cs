using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Intent;
using AliceNeural.Utils;
using AliceNeural.Models;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Web;
using HttpProxyControl;
using System.Globalization;
using System.Security.Cryptography;
using static System.Collections.Specialized.BitVector32;
//using Java.Net;
//using static Android.Graphics.ColorSpace;
//using AndroidX.Core.Content;
namespace AliceNeural
{
    public partial class MainPage : ContentPage
    {
        static HttpClient client = HttpProxyHelper.CreateHttpClient(setProxy: true);
        SpeechRecognizer? speechRecognizer;
        IntentRecognizer? intentRecognizerByPatternMatching;
        IntentRecognizer? intentRecognizerByCLU;
        SpeechSynthesizer? speechSynthesizer;
        TaskCompletionSourceManager<int>? taskCompletionSourceManager;
        AzureCognitiveServicesResourceManager? serviceManager;
        bool buttonToggle = false;
        Brush? buttonToggleColor;
        private static readonly JsonSerializerOptions? jsonSerializationOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
        public MainPage()
        {
            InitializeComponent();
            serviceManager = new AzureCognitiveServicesResourceManager("MyResponder1", "Train2");
            taskCompletionSourceManager = new TaskCompletionSourceManager<int>();
            (intentRecognizerByPatternMatching, speechSynthesizer, intentRecognizerByCLU) =
                ConfigureContinuousIntentPatternMatchingWithMicrophoneAsync(
                    serviceManager.CurrentSpeechConfig,
                    serviceManager.CurrentCluModel,
                    serviceManager.CurrentPatternMatchingModel,
                    taskCompletionSourceManager);
            speechRecognizer = new SpeechRecognizer(serviceManager.CurrentSpeechConfig);
        }
        protected override async void OnDisappearing()
        {
            base.OnDisappearing();

            if (speechSynthesizer != null)
            {
                await speechSynthesizer.StopSpeakingAsync();
                speechSynthesizer.Dispose();
            }

            if (intentRecognizerByPatternMatching != null)
            {
                await intentRecognizerByPatternMatching.StopContinuousRecognitionAsync();
                intentRecognizerByPatternMatching.Dispose();
            }

            if (intentRecognizerByCLU != null)
            {
                await intentRecognizerByCLU.StopContinuousRecognitionAsync();
                intentRecognizerByCLU.Dispose();
            }
        }

        private async void ContentPage_Loaded(object sender, EventArgs e)
        {
            await CheckAndRequestMicrophonePermission();
        }

        private async Task<PermissionStatus> CheckAndRequestMicrophonePermission()
        {
            PermissionStatus status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
            if (status == PermissionStatus.Granted)
            {
                return status;
            }
            if (Permissions.ShouldShowRationale<Permissions.Microphone>())
            {
                // Prompt the user with additional information as to why the permission is needed
                await DisplayAlert("Permission required", "Microphone permission is necessary", "OK");
            }
            status = await Permissions.RequestAsync<Permissions.Microphone>();
            return status;
        }

        private static async Task ContinuousIntentPatternMatchingWithMicrophoneAsync(
            IntentRecognizer intentRecognizer, TaskCompletionSourceManager<int> stopRecognition)
        {
            await intentRecognizer.StartContinuousRecognitionAsync();
            // Waits for completion. Use Task.WaitAny to keep the task rooted.
            Task.WaitAny(new[] { stopRecognition.TaskCompletionSource.Task });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="config"></param>
        /// <param name="cluModel"></param>
        /// <param name="patternMatchingModelCollection"></param>
        /// <param name="stopRecognitionManager"></param>
        /// <returns>una tupla contentente nell'ordine un intent recognizer basato su Patter Matching, un sintetizzatore vocale e un intent recognizer basato su un modello di Conversational Language Understanding </returns>
        private static (IntentRecognizer, SpeechSynthesizer, IntentRecognizer) ConfigureContinuousIntentPatternMatchingWithMicrophoneAsync(
            SpeechConfig config,
            ConversationalLanguageUnderstandingModel cluModel,
            LanguageUnderstandingModelCollection patternMatchingModelCollection,
            TaskCompletionSourceManager<int> stopRecognitionManager)
        {
            //creazione di un intent recognizer basato su pattern matching
            var intentRecognizerByPatternMatching = new IntentRecognizer(config);
            intentRecognizerByPatternMatching.ApplyLanguageModels(patternMatchingModelCollection);

            //creazione di un intent recognizer basato su CLU
            var intentRecognizerByCLU = new IntentRecognizer(config);
            var modelsCollection = new LanguageUnderstandingModelCollection { cluModel };
            intentRecognizerByCLU.ApplyLanguageModels(modelsCollection);

            //creazione di un sitetizzatore vocale
            var synthesizer = new SpeechSynthesizer(config);

            //gestione eventi
            intentRecognizerByPatternMatching.Recognized += async (s, e) =>
            {
                switch (e.Result.Reason)
                {
                    case ResultReason.RecognizedSpeech:
                        Debug.WriteLine($"PATTERN MATCHING - RECOGNIZED SPEECH: Text= {e.Result.Text}");
                        break;
                    case ResultReason.RecognizedIntent:
                        {
                            Debug.WriteLine($"PATTERN MATCHING - RECOGNIZED INTENT: Text= {e.Result.Text}");
                            Debug.WriteLine($"       Intent Id= {e.Result.IntentId}.");
                            if (e.Result.IntentId == "Ok")
                            {
                                Debug.WriteLine("Stopping current speaking if any...");
                                await synthesizer.StopSpeakingAsync();
                                Debug.WriteLine("Stopping current intent recognition by CLU if any...");
                                await intentRecognizerByCLU.StopContinuousRecognitionAsync();
                                await HandleOkCommand(synthesizer, intentRecognizerByCLU).ConfigureAwait(false);
                            }
                            else if (e.Result.IntentId == "Stop")
                            {
                                Debug.WriteLine("Stopping current speaking...");
                                await synthesizer.StopSpeakingAsync();
                            }
                        }

                        break;
                    case ResultReason.NoMatch:
                        Debug.WriteLine($"NOMATCH: Speech could not be recognized.");
                        var noMatch = NoMatchDetails.FromResult(e.Result);
                        switch (noMatch.Reason)
                        {
                            case NoMatchReason.NotRecognized:
                                Debug.WriteLine($"PATTERN MATCHING - NOMATCH: Speech was detected, but not recognized.");
                                break;
                            case NoMatchReason.InitialSilenceTimeout:
                                Debug.WriteLine($"PATTERN MATCHING - NOMATCH: The start of the audio stream contains only silence, and the service timed out waiting for speech.");
                                break;
                            case NoMatchReason.InitialBabbleTimeout:
                                Debug.WriteLine($"PATTERN MATCHING - NOMATCH: The start of the audio stream contains only noise, and the service timed out waiting for speech.");
                                break;
                            case NoMatchReason.KeywordNotRecognized:
                                Debug.WriteLine($"PATTERN MATCHING - NOMATCH: Keyword not recognized");
                                break;
                        }
                        break;

                    default:
                        break;
                }
            };
            intentRecognizerByPatternMatching.Canceled += (s, e) =>
            {
                Debug.WriteLine($"PATTERN MATCHING - CANCELED: Reason={e.Reason}");

                if (e.Reason == CancellationReason.Error)
                {
                    Debug.WriteLine($"PATTERN MATCHING - CANCELED: ErrorCode={e.ErrorCode}");
                    Debug.WriteLine($"PATTERN MATCHING - CANCELED: ErrorDetails={e.ErrorDetails}");
                    Debug.WriteLine($"PATTERN MATCHING - CANCELED: Did you update the speech key and location/region info?");
                }
                stopRecognitionManager.TaskCompletionSource.TrySetResult(0);
            };
            intentRecognizerByPatternMatching.SessionStopped += (s, e) =>
            {
                Debug.WriteLine("\n    Session stopped event.");
                stopRecognitionManager.TaskCompletionSource.TrySetResult(0);
            };

            return (intentRecognizerByPatternMatching, synthesizer, intentRecognizerByCLU);

        }
        private static async Task HandleOkCommand(SpeechSynthesizer synthesizer, IntentRecognizer intentRecognizer)
        {
            await synthesizer.SpeakTextAsync("Sono in ascolto");
            //avvia l'intent recognition su Azure
            string? jsonResult = await RecognizeIntentAsync(intentRecognizer);
            if (jsonResult != null)
            {
                //process jsonResult
                //deserializzo il json
                CLUResponse cluResponse = JsonSerializer.Deserialize<CLUResponse>(jsonResult, jsonSerializationOptions) ?? new CLUResponse();
                await synthesizer.SpeakTextAsync($"La tua richiesta è stata {cluResponse.Result?.Query}");
                var topIntent = cluResponse.Result?.Prediction?.TopIntent;
                var tipoIntent = cluResponse.Result?.Prediction?.Intents?[0].Category;

                if (topIntent != null)
                {
                    switch (topIntent)
                    {
                        case string intent when intent.Contains("Wiki"):
                            string? ricerca = null, sezione = null;
                            //await synthesizer.SpeakTextAsync("Vuoi fare una ricerca su Wikipedia");
                            foreach (var item in cluResponse.Result?.Prediction?.Entities)
                            {
                                if (item.Category == "Wiki.MainSearch")
                                {
                                    ricerca = item.Text.ToLower();
                                }

                                if (item.Category == "Wiki.SubItemSearch")
                                {
                                    sezione = item.Text.ToLower();
                                }
                            }
                            await WikiSearch(ricerca, sezione, synthesizer);
                            break;

                        case string intent when intent.Contains("Weather"):
                            //await synthesizer.SpeakTextAsync("Vuoi sapere come è il tempo");
                            string? luogo = null, condizione = null, tempo = null;
                            foreach (var item in cluResponse.Result?.Prediction?.Entities)
                            {
                                if (item.Category.Contains("datetimeV2"))
                                    tempo = item.Text;

                                else if (item.Category.Contains("Places"))
                                    luogo = item.Text;

                                else if (item.Category.Contains("Weather"))
                                    condizione = item.Text;
                            }
                            await OpenMeteo(condizione, luogo, tempo, synthesizer);
                            break;

                        case string intent when intent.Contains("Places"):
                            string? tipoLuogo = null, dove1 = null, dove2 = null;
                            foreach (var item in cluResponse.Result?.Prediction?.Entities)
                            {
                                if (item.Category.Contains("Nearby") || item.Category.Contains("AbsoluteLocation") || item.Category.Contains("PlaceName"))
                                {
                                    if (dove1 == null)
                                    {
                                        dove1 = item.Text;
                                    }
                                    else
                                    {
                                        dove2 = item.Text;
                                    }
                                }
                                if (item.Category.Contains("PlaceType"))
                                    tipoLuogo = item.Text;
                            }
                            string query = cluResponse.Result.Query;
                            await BingMaps(tipoLuogo, dove1, dove2, synthesizer);
                            break;

                        case string intent when intent.Contains("None"):
                            await synthesizer.SpeakTextAsync("Non ho capito");
                            break;
                    }

                }
                //determino l'action da fare, eventualmente effettuando una richiesta GET su un endpoint remoto scelto in base al topScoringIntent
                //ottengo il risultato dall'endpoit remoto
                //effettuo un text to speech per descrivere il risultato



            }
            else
            {
                //è stato restituito null - ad esempio quando il processo è interrotto prima di ottenre la risposta dal server
                Debug.WriteLine("Non è stato restituito nulla dall'intent reconition sul server");
            }
        }

        public static async Task<string?> RecognizeIntentAsync(IntentRecognizer recognizer)
        {
            // Starts recognizing.
            Debug.WriteLine("Say something...");

            // Starts intent recognition, and returns after a single utterance is recognized. The end of a
            // single utterance is determined by listening for silence at the end or until a maximum of 15
            // seconds of audio is processed.  The task returns the recognition text as result. 
            // Note: Since RecognizeOnceAsync() returns only a single utterance, it is suitable only for single
            // shot recognition like command or query. 
            // For long-running multi-utterance recognition, use StartContinuousRecognitionAsync() instead.
            var result = await recognizer.RecognizeOnceAsync();
            string? languageUnderstandingJSON = null;

            // Checks result.
            switch (result.Reason)
            {
                case ResultReason.RecognizedIntent:
                    Debug.WriteLine($"RECOGNIZED: Text={result.Text}");
                    Debug.WriteLine($"    Intent Id: {result.IntentId}.");
                    languageUnderstandingJSON = result.Properties.GetProperty(PropertyId.LanguageUnderstandingServiceResponse_JsonResult);
                    Debug.WriteLine($"    Language Understanding JSON: {languageUnderstandingJSON}.");
                    CLUResponse cluResponse = JsonSerializer.Deserialize<CLUResponse>(languageUnderstandingJSON, jsonSerializationOptions) ?? new CLUResponse();
                    Debug.WriteLine("Risultato deserializzato:");
                    Debug.WriteLine($"kind: {cluResponse.Kind}");
                    Debug.WriteLine($"result.query: {cluResponse.Result?.Query}");
                    Debug.WriteLine($"result.prediction.topIntent: {cluResponse.Result?.Prediction?.TopIntent}");
                    Debug.WriteLine($"result.prediction.Intents[0].Category: {cluResponse.Result?.Prediction?.Intents?[0].Category}");
                    Debug.WriteLine($"result.prediction.Intents[0].ConfidenceScore: {cluResponse.Result?.Prediction?.Intents?[0].ConfidenceScore}");
                    Debug.WriteLine($"result.prediction.entities: ");
                    cluResponse.Result?.Prediction?.Entities?.ForEach(s => Debug.WriteLine($"\tcategory = {s.Category}; text= {s.Text};"));
                    break;
                case ResultReason.RecognizedSpeech:
                    Debug.WriteLine($"RECOGNIZED: Text={result.Text}");
                    Debug.WriteLine($"    Intent not recognized.");
                    break;
                case ResultReason.NoMatch:
                    Debug.WriteLine($"NOMATCH: Speech could not be recognized.");
                    break;
                case ResultReason.Canceled:
                    var cancellation = CancellationDetails.FromResult(result);
                    Debug.WriteLine($"CANCELED: Reason={cancellation.Reason}");

                    if (cancellation.Reason == CancellationReason.Error)
                    {
                        Debug.WriteLine($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                        Debug.WriteLine($"CANCELED: ErrorDetails={cancellation.ErrorDetails}");
                        Debug.WriteLine($"CANCELED: Did you update the subscription info?");
                    }
                    break;
            }
            return languageUnderstandingJSON;
        }
        private async void OnRecognitionButtonClicked2(object sender, EventArgs e)
        {
            if (serviceManager != null && taskCompletionSourceManager != null)
            {
                buttonToggle = !buttonToggle;
                if (buttonToggle)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        buttonToggleColor = RecognizeSpeechBtn.Background;
                    });

                    RecognizeSpeechBtn.Background = Colors.Yellow;
                    //creo le risorse
                    //su un dispositivo mobile potrebbe succedere che cambiando rete cambino i parametri della rete, ed in particolare il proxy
                    //In questo caso, per evitare controlli troppo complessi, si è scelto di ricreare lo speechConfig ad ogni richiesta se cambia il proxy
                    if (serviceManager.ShouldRecreateSpeechConfigForProxyChange())
                    {
                        (intentRecognizerByPatternMatching, speechSynthesizer, intentRecognizerByCLU) =
                       ConfigureContinuousIntentPatternMatchingWithMicrophoneAsync(
                           serviceManager.CurrentSpeechConfig,
                           serviceManager.CurrentCluModel,
                           serviceManager.CurrentPatternMatchingModel,
                           taskCompletionSourceManager);
                    }

                    _ = Task.Factory.StartNew(async () =>
                    {
                        taskCompletionSourceManager.TaskCompletionSource = new TaskCompletionSource<int>();
                        await ContinuousIntentPatternMatchingWithMicrophoneAsync(
                            intentRecognizerByPatternMatching!, taskCompletionSourceManager)
                        .ConfigureAwait(false);
                    });
                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        RecognizeSpeechBtn.Background = buttonToggleColor;
                    });
                    //la doppia chiamata di StopSpeakingAsync è un work-around a un problema riscontrato in alcune situazioni:
                    //se si prova a fermare il task mentre il sintetizzatore sta parlando, in alcuni casi si verifica un'eccezione. 
                    //Con il doppio StopSpeakingAsync non succede.
                    await speechSynthesizer!.StopSpeakingAsync();
                    await speechSynthesizer.StopSpeakingAsync();
                    await intentRecognizerByCLU!.StopContinuousRecognitionAsync();
                    await intentRecognizerByPatternMatching!.StopContinuousRecognitionAsync();
                    //speechSynthesizer.Dispose();
                    //intentRecognizerByPatternMatching.Dispose();
                }
            }
        }
        private async void OnRecognitionButtonClicked(object sender, EventArgs e)
        {
            try
            {
                //accedo ai servizi
                //AzureCognitiveServicesResourceManager serviceManager =(Application.Current as App).AzureCognitiveServicesResourceManager;
                // Creates a speech recognizer using microphone as audio input.
                // Starts speech recognition, and returns after a single utterance is recognized. The end of a
                // single utterance is determined by listening for silence at the end or until a maximum of 15
                // seconds of audio is processed.  The task returns the recognition text as result.
                // Note: Since RecognizeOnceAsync() returns only a single utterance, it is suitable only for single
                // shot recognition like command or query.
                // For long-running multi-utterance recognition, use StartContinuousRecognitionAsync() instead.
                var result = await speechRecognizer!.RecognizeOnceAsync().ConfigureAwait(false);

                // Checks result.
                StringBuilder sb = new();
                if (result.Reason == ResultReason.RecognizedSpeech)
                {
                    sb.AppendLine($"RECOGNIZED: Text={result.Text}");
                    await speechSynthesizer!.SpeakTextAsync(result.Text);
                }
                else if (result.Reason == ResultReason.NoMatch)
                {
                    sb.AppendLine($"NOMATCH: Speech could not be recognized.");
                }
                else if (result.Reason == ResultReason.Canceled)
                {
                    var cancellation = CancellationDetails.FromResult(result);
                    sb.AppendLine($"CANCELED: Reason={cancellation.Reason}");

                    if (cancellation.Reason == CancellationReason.Error)
                    {
                        sb.AppendLine($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                        sb.AppendLine($"CANCELED: ErrorDetails={cancellation.ErrorDetails}");
                        sb.AppendLine($"CANCELED: Did you update the subscription info?");
                    }
                }
                UpdateUI(sb.ToString());
            }
            catch (Exception ex)
            {
                UpdateUI("Exception: " + ex.ToString());
            }
        }


        #region OPENMETEO
        public static async Task OpenMeteo(string? condizione, string? luogo, string? tempo, SpeechSynthesizer synthesizer)
        {
            string datoNonFornito = "";
            if (luogo == null)
                luogo = "Monticello Brianza";

            var coordinate = await TrovaCoordinate(luogo);

            DateOnly data = DateOnly.FromDateTime(DateTime.Today);
            DateOnly dataFine = data;
            string? dataDaStampare = "";

            if (tempo == null)
            {
                dataFine = dataFine.AddDays(6);
            }
            else if (tempo.ToLower() == "dopodomani")
            {
                data = data.AddDays(2);
                dataFine = data;
                dataDaStampare = tempo;
            }
            else if (tempo.ToLower() == "domani")
            {
                data = data.AddDays(1);
                dataFine = data;
                dataDaStampare = tempo;
            }
            else if (tempo.ToLower() == "oggi")
            {
                dataFine = data;
                dataDaStampare = tempo;
            }
            else if (tempo.ToLower().Contains("giorn"))
            {
                string[] quantiGiorni = tempo.Split(' ');
                switch (quantiGiorni[0])
                {
                    case "un" or "1":
                        data = data.AddDays(1);
                        dataFine = data;
                        break;

                    case "due" or "2":
                        data = data.AddDays(2);
                        dataFine = data;
                        break;

                    case "tre" or "3":
                        data = data.AddDays(3);
                        dataFine = data;
                        break;

                    case "quattro" or "4":
                        data = data.AddDays(4);
                        dataFine = data;
                        break;

                    case "cinque" or "5":
                        data = data.AddDays(5);
                        dataFine = data;
                        break;

                    case "sei" or "6":
                        data = data.AddDays(6);
                        dataFine = data;
                        break;

                    case "sette" or "7": 
                        data = data.AddDays(7);
                        dataFine = data;
                        break;

                    case "otto" or "8":
                        data = data.AddDays(8);
                        dataFine = data;
                        break;

                    case "nove" or "9":
                        data = data.AddDays(9);
                        dataFine = data;
                        break;

                    case "dieci" or "10":
                        data = data.AddDays(10);
                        dataFine = data;
                        break;

                    default:
                        await synthesizer.SpeakTextAsync("Numero di giorni troppo grande");
                        Debug.WriteLine("Numero di giorni troppo grande");
                        return;
                }
                dataDaStampare = "tra " + tempo;
            }
            else
            {
                data = DateOnly.Parse(tempo);
                dataFine = data;
                dataDaStampare = "il " + data.ToLongDateString();
            }

            string dataOk = data.ToString("yyyy-MM-dd");
            string dataFineOk = dataFine.ToString("yyyy-MM-dd");
            FormattableString addressUrlFormattable;
            addressUrlFormattable = $"https://api.open-meteo.com/v1/forecast?latitude={coordinate?.lat.ToString().Replace(",", ".")}&longitude={coordinate?.lon.ToString().Replace(",", ".")}&current=temperature_2m,weather_code,wind_speed_10m,wind_direction_10m&hourly=temperature_2m,relative_humidity_2m,dew_point_2m,apparent_temperature,precipitation_probability,precipitation,rain,showers,weather_code,wind_speed_10m,wind_direction_10m&daily=weather_code,temperature_2m_max,temperature_2m_min,apparent_temperature_max,apparent_temperature_min&timeformat=unixtime&timezone=auto&start_date={dataOk}&end_date={dataFineOk}";
            Debug.WriteLine(addressUrlFormattable);
            string addressUrl = FormattableString.Invariant(addressUrlFormattable);
            HttpResponseMessage response = await client.GetAsync($"{addressUrl}");
            if (response.IsSuccessStatusCode)
            {
                bool piove = false;
                bool nevica = false;
                bool sole = false;
                double? uTot = 0.0;
                DateOnly data1 = data;
                string[] quandoSuccede = new string[7];
                int cont = 0;
                Forecast? forecast = await response.Content.ReadFromJsonAsync<Forecast>();
                if (forecast != null)
                {
                    foreach (var item in forecast.daily.weather_code)
                    {
                        switch (condizione.ToLower())
                        {
                            case "soleggiato" or "sole":
                                if (item >= 0 && item <= 2)
                                {
                                    quandoSuccede[cont] = "il " + data1.ToLongDateString();
                                    if (tempo != "oggi" && tempo != "domani" && tempo != "dopodomani")
                                        dataDaStampare = quandoSuccede[cont];
                                    sole = true;
                                }
                                break;

                            case "pioverà" or "piove":
                                if (item >= 45 && item <= 65 || item >= 80 && item <= 99)
                                {
                                    quandoSuccede[cont] = "il " + data1.ToLongDateString(); 
                                    if (tempo != "oggi" && tempo != "domani" && tempo != "dopodomani")
                                        dataDaStampare = quandoSuccede[cont];
                                    piove = true;
                                }
                                break;

                            case "nevicherà" or "nevica":
                                if (item >= 66 && item <= 77)
                                {
                                    quandoSuccede[cont] = "il " + data1.ToLongDateString();
                                    if (tempo != "oggi" && tempo != "domani" && tempo != "dopodomani")
                                        dataDaStampare = quandoSuccede[cont];
                                    nevica = true;
                                }
                                break;

                            case "umidità":
                                if (data == dataFine)
                                {
                                    foreach (var item2 in forecast.hourly.relative_humidity_2m)
                                    {
                                        uTot += item2;
                                    }
                                    uTot = uTot / 24;
                                }
                                break;

                            default:
                                break;
                        }
                        if (quandoSuccede[cont] != null)
                        {
                            data1.AddDays(1);
                            cont++;
                        }
                    }

                    switch (condizione.ToLower())
                    {
                        case "soleggiato" or "sole":
                            if (tempo != null)
                            {
                                if (sole)
                                    await synthesizer.SpeakTextAsync($"sì, sarà soleggiato a {luogo} {dataDaStampare}");
                                else
                                    await synthesizer.SpeakTextAsync($"no, non sarà soleggiato a {luogo} {dataDaStampare}, il meteo sarà invece {Utilss.Display(Utilss.WMOCodesIntIT(forecast.daily.weather_code[0]), datoNonFornito)}");
                            }
                            else
                            {
                                if (sole)
                                    await synthesizer.SpeakTextAsync($"a {luogo} sarà soleggiato {dataDaStampare}");
                                else
                                    await synthesizer.SpeakTextAsync($"nei prossimi 7 giorni a {luogo} non sarà soleggiato");
                            }
                            break;

                        case "pioverà" or "piove":
                            if (tempo != null)
                            {
                                if (piove)
                                    await synthesizer.SpeakTextAsync($"sì, pioverà a {luogo} {dataDaStampare}");
                                else
                                    await synthesizer.SpeakTextAsync($"no, non pioverà a {luogo} {dataDaStampare}, il meteo sarà invece {Utilss.Display(Utilss.WMOCodesIntIT(forecast.daily.weather_code[0]), datoNonFornito)}");
                            }
                            else
                            {
                                if (piove)
                                    await synthesizer.SpeakTextAsync($"a {luogo} pioverà {dataDaStampare}");
                                else
                                    await synthesizer.SpeakTextAsync($"nei prossimi 7 giorni a {luogo} non pioverà");
                            }
                            break;

                        case "nevicherà" or "nevica":
                            if (tempo != null)
                            {
                                if (nevica)
                                    await synthesizer.SpeakTextAsync($"sì, nevicherà a {luogo} {dataDaStampare}");
                                else
                                    await synthesizer.SpeakTextAsync($"no, non nevicherà a {luogo} {dataDaStampare}, il meteo sarà invece {Utilss.Display(Utilss.WMOCodesIntIT(forecast.daily.weather_code[0]), datoNonFornito)}");
                            }
                            else
                            {
                                if (nevica)
                                    await synthesizer.SpeakTextAsync($"a {luogo} nevicherà {dataDaStampare}");
                                else
                                    await synthesizer.SpeakTextAsync($"nei prossimi 7 giorni a {luogo} non nevicherà");
                            }
                            break;

                        case "umidità":
                            if (data == dataFine)
                            {
                                await synthesizer.SpeakTextAsync($"{dataDaStampare} a {luogo} ci sarà una umidità media del {uTot} %");
                            }
                            else
                            {
                                await synthesizer.SpeakTextAsync($"Errore, specificare un giorno preciso");
                            }
                            break;

                        case "temperatura":
                            await synthesizer.SpeakTextAsync($"{dataDaStampare} a {luogo} ci sarà una " +
                                    $"temperatura massima di {forecast.daily.temperature_2m_max.First()} gradi e una minima di {forecast.daily.temperature_2m_min.First()} gradi");
                            break;

                        case "tempo" or "meteo":
                            await synthesizer.SpeakTextAsync($"{dataDaStampare} a {luogo} " +
                                $"il meteo sarà {Utilss.Display(Utilss.WMOCodesIntIT(forecast.daily.weather_code[0]), datoNonFornito)} " +
                                    $" con una temperatura massima di {forecast.daily.temperature_2m_max.First()} gradi e una minima di {forecast.daily.temperature_2m_min.First()}°");
                            break;


                        default:
                            break;
                    }
                }
            }
        }
        #endregion

        #region BINGMAPS
        public static async Task BingMaps(string? tipoLuogo, string? dove1, string dove2, SpeechSynthesizer synthesizer)
        {
            if (dove1.Contains("vicino a me"))
            {
                dove1 = "monticello brianza";
            }

            if (tipoLuogo != null)
                await FindPlaces(tipoLuogo, dove1, synthesizer);
            else
            {
                if (dove2 != null)
                {
                    await GetDistance(dove1, dove2, synthesizer);
                }
                else
                {
                    await GetDistance("monticello brianza", dove1, synthesizer);
                }
            }
        }
        public static async Task GetDistance(string? partenza, string? arrivo, SpeechSynthesizer synthesizer)
        {
            string partenzaEncode = HttpUtility.UrlEncode(partenza);
            string arrivoEncode = HttpUtility.UrlEncode(arrivo);
            string url = $"https://dev.virtualearth.net/REST/v1/Routes?wp.1={partenzaEncode}&wp.2={arrivoEncode}&optimize=time&tt=departure&dt=2024-04-11%2019:35:00&distanceUnit=km&c=it&ra=regionTravelSummary&key=Al4ZtPs1VEriHmYisihtlQUg8ZUNQqtE_3vzIjQo69p6fCElsdPvxAkkJ5nLIxHl";
            HttpResponseMessage response = await client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                LocalRoute? localRoute = await response.Content.ReadFromJsonAsync<LocalRoute>();
                if (localRoute != null)
                {
                    // distanza in km
                    double? distanza = localRoute.ResourceSets[0].Resources[0].TravelDistance;
                    double durata = localRoute.ResourceSets[0].Resources[0].TravelDuration;
                    double durataConTraffico = localRoute.ResourceSets[0].Resources[0].TravelDurationTraffic;
                    string modViaggio = localRoute.ResourceSets[0].Resources[0].TravelMode;
                    Debug.WriteLine($"La distanza da {partenza} a {arrivo} è di {distanza} KM" +
                        $"\ncon una durata di {durata / 60} minuti o " +
                        $"con {durataConTraffico / 60} minuti con il traffico attuale utilizzando {modViaggio} ");

                    await synthesizer.SpeakTextAsync($"La distanza da {partenza} a {arrivo} è di {distanza:F1} chilometri con una durata di {(durata / 60):F0} minuti o con {(durataConTraffico / 60):F0} minuti con il traffico attuale utilizzando {modViaggio}");
                }
            }
        }
        public static async Task FindPlaces(string? tipoLuogo, string? luogo, SpeechSynthesizer synthesizer)
        {
            var coordinate = await TrovaCoordinate(luogo);
            string? lat = $"{coordinate?.lat}";
            string? lon = $"{coordinate?.lon}";
            lat = lat.Replace(",", ".");
            lon = lon.Replace(",", ".");
            FormattableString urlComplete = $"https://dev.virtualearth.net/REST/v1/LocationRecog/{lat},{lon}?radius=1&top=15&datetime=2024-04-11%2018:50:42Z&distanceunit=km&verboseplacenames=true&includeEntityTypes=businessAndPOI,naturalPOI,address&includeNeighborhood=1&include=ciso2&key=Al4ZtPs1VEriHmYisihtlQUg8ZUNQqtE_3vzIjQo69p6fCElsdPvxAkkJ5nLIxHl";
            string? ristoranti = "Buffet Restaurants Cafe Restaurants Chinese Restaurants Diners Italian Restaurants Japanese Restaurants Mexican Restaurants Pizza Restaurants Sandwiches Seafood Restaurants Steak House Restaurants Sushi Restaurants Take Away Taverns Vegetarian And VeganRestaurants";
            string? bar = "Bars Bars Grills And Pubs Desserts Ice Cream Parlors Cocktail Lounges Coffee And Tea Sports Bars B2B Agriculture and Food B2B Food Products";
            string? supermercati = "Supermarkets Liquor Stores Grocery Grocers Discount Stores Fish and Meat Markets Farmers Markets";
            string? negozi = "Construction Services Automotive and Vehicles Cars and Trucks Book stores Real Estate Rental Services Beauty and Spa Business-to-Business Health and Beauty Supplies CD And Record Stores Cigar And Tobacco Shops Discount Stores Furniture Stores Home Improvement Stores Jewelry And Watches Stores Liquor Stores Malls And Shopping Centers Music Stores Outlet Stores Pet Shops Pet Supply Stores School And Office Supply Stores Shoe Stores Sporting Goods Stores Toy And Game Stores";
            string? attrazioni = "Amusement Parks Attractions Carnivals Casinos Landmarks And Historical Sites Movie Theaters Museums Parks Zoos";
            string? fastFood = "Fast Food";
            string? ospedali = "Hospitals";
            string? hotel = "Hotels And Motels";
            string? parcheggi = "Parking";
            string? addressUrl = FormattableString.Invariant(urlComplete);
            HttpResponseMessage response = await client.GetAsync(addressUrl);
            if (response.IsSuccessStatusCode)
            {
                if (tipoLuogo != null || tipoLuogo == "")
                {
                    LocalRecognition? data = await response.Content.ReadFromJsonAsync<LocalRecognition>();
                    bool primo = true;
                    string luogoTrovato = "";
                    switch (tipoLuogo.ToLower())
                    {
                        case "ristoranti" or "ristorante":
                            luogoTrovato = "";
                            foreach (var business in data?.ResourceSets?[0]?.Resources?[0]?.BusinessesAtLocation)
                            {
                                for (int i = 0; i < business?.BusinessInfo?.OtherTypes?.Count; i++)
                                {
                                    if (ristoranti.Contains(business?.BusinessInfo?.OtherTypes?[i]))
                                    {
                                        if (primo == true)
                                        {
                                            await synthesizer.SpeakTextAsync($"Ristoranti a {luogo}");
                                            primo = false;
                                        }
                                        if (business?.BusinessInfo?.EntityName != luogoTrovato)
                                        {
                                            luogoTrovato = business.BusinessInfo.EntityName;
                                            await synthesizer.SpeakTextAsync(luogoTrovato);
                                            Debug.WriteLine(luogoTrovato);
                                        }
                                    }
                                }
                            }
                            break;

                        case "bar":
                            luogoTrovato = "";
                            foreach (var business in data?.ResourceSets?[0]?.Resources?[0]?.BusinessesAtLocation)
                            {
                                for (int i = 0; i < business?.BusinessInfo?.OtherTypes?.Count; i++)
                                {
                                    if (bar.Contains(business?.BusinessInfo?.OtherTypes?[i]))
                                    {
                                        if (primo == true)
                                        {
                                            await synthesizer.SpeakTextAsync($"Bar a {luogo}");
                                            primo = false;
                                        }
                                        if (business?.BusinessInfo?.EntityName != luogoTrovato)
                                        {
                                            luogoTrovato = business.BusinessInfo.EntityName;
                                            await synthesizer.SpeakTextAsync(luogoTrovato);
                                            Debug.WriteLine(luogoTrovato);
                                        }
                                    }
                                }
                            }
                            break;

                        case "supermercati" or "supermercato":
                            luogoTrovato = "";
                            foreach (var business in data?.ResourceSets?[0]?.Resources?[0]?.BusinessesAtLocation)
                            {
                                for (int i = 0; i < business?.BusinessInfo?.OtherTypes?.Count; i++)
                                {
                                    if (supermercati.Contains(business?.BusinessInfo?.OtherTypes?[i]))
                                    {
                                        if (primo == true)
                                        {
                                            await synthesizer.SpeakTextAsync($"Supermercati a {luogo}");
                                            primo = false;
                                        }
                                        if (business?.BusinessInfo?.EntityName != luogoTrovato)
                                        {
                                            luogoTrovato = business.BusinessInfo.EntityName;
                                            await synthesizer.SpeakTextAsync(luogoTrovato);
                                            Debug.WriteLine(luogoTrovato);
                                        }
                                    }
                                }
                            }
                            break;

                        case "negozio" or "negozi":
                            luogoTrovato = "";
                            foreach (var business in data?.ResourceSets?[0]?.Resources?[0]?.BusinessesAtLocation)
                            {
                                for (int i = 0; i < business?.BusinessInfo?.OtherTypes?.Count; i++)
                                {
                                    if (negozi.Contains(business?.BusinessInfo?.OtherTypes?[i]))
                                    {
                                        if (primo == true)
                                        {
                                            await synthesizer.SpeakTextAsync($"Negozi a {luogo}");
                                            primo = false;
                                        }
                                        if (business?.BusinessInfo?.EntityName != luogoTrovato)
                                        {
                                            luogoTrovato = business.BusinessInfo.EntityName;
                                            await synthesizer.SpeakTextAsync(luogoTrovato);
                                            Debug.WriteLine(luogoTrovato);
                                        }
                                    }
                                }
                            }
                            break;

                        case "attrazioni" or "attrazione" or "cinema" or "parco" or "parchi" or "museo" or "musei":
                            luogoTrovato = "";
                            foreach (var business in data?.ResourceSets?[0]?.Resources?[0]?.BusinessesAtLocation)
                            {
                                for (int i = 0; i < business?.BusinessInfo?.OtherTypes?.Count; i++)
                                {
                                    if (attrazioni.Contains(business?.BusinessInfo?.OtherTypes?[i]))
                                    {
                                        if (primo == true)
                                        {
                                            await synthesizer.SpeakTextAsync($"Attrazioni a {luogo}");
                                            primo = false;
                                        }
                                        if (business?.BusinessInfo?.EntityName != luogoTrovato)
                                        {
                                            luogoTrovato = business.BusinessInfo.EntityName;
                                            await synthesizer.SpeakTextAsync(luogoTrovato);
                                            Debug.WriteLine(luogoTrovato);
                                        }
                                    }
                                }
                            }
                            break;

                        case "fastfood":
                            luogoTrovato = "";
                            foreach (var business in data?.ResourceSets?[0]?.Resources?[0]?.BusinessesAtLocation)
                            {
                                for (int i = 0; i < business?.BusinessInfo?.OtherTypes?.Count; i++)
                                {
                                    if (fastFood.Contains(business?.BusinessInfo?.OtherTypes?[i]))
                                    {
                                        if (primo == true)
                                        {
                                            await synthesizer.SpeakTextAsync($"Fastfood a {luogo}");
                                            primo = false;
                                        }
                                        if (business?.BusinessInfo?.EntityName != luogoTrovato)
                                        {
                                            luogoTrovato = business.BusinessInfo.EntityName;
                                            await synthesizer.SpeakTextAsync(luogoTrovato);
                                            Debug.WriteLine(luogoTrovato);
                                        }
                                    }
                                }
                            }
                            break;

                        case "ospedali" or "ospedale":
                            luogoTrovato = "";
                            foreach (var business in data?.ResourceSets?[0]?.Resources?[0]?.BusinessesAtLocation)
                            {
                                for (int i = 0; i < business?.BusinessInfo?.OtherTypes?.Count; i++)
                                {
                                    if (ospedali.Contains(business?.BusinessInfo?.OtherTypes?[i]))
                                    {
                                        if (primo == true)
                                        {
                                            await synthesizer.SpeakTextAsync($"Ospedali a {luogo}");
                                            primo = false;
                                        }
                                        if (business?.BusinessInfo?.EntityName != luogoTrovato)
                                        {
                                            luogoTrovato = business.BusinessInfo.EntityName;
                                            await synthesizer.SpeakTextAsync(luogoTrovato);
                                            Debug.WriteLine(luogoTrovato);
                                        }
                                    }
                                }
                            }
                            break;

                        case "hotel":
                            luogoTrovato = "";
                            foreach (var business in data?.ResourceSets?[0]?.Resources?[0]?.BusinessesAtLocation)
                            {
                                for (int i = 0; i < business?.BusinessInfo?.OtherTypes?.Count; i++)
                                {
                                    if (hotel.Contains(business?.BusinessInfo?.OtherTypes?[i]))
                                    {
                                        if (primo == true)
                                        {
                                            await synthesizer.SpeakTextAsync($"Hotel a {luogo}");
                                            primo = false;
                                        }
                                        if (business?.BusinessInfo?.EntityName != luogoTrovato)
                                        {
                                            luogoTrovato = business.BusinessInfo.EntityName;
                                            await synthesizer.SpeakTextAsync(luogoTrovato);
                                            Debug.WriteLine(luogoTrovato);
                                        }
                                    }
                                }
                            }
                            break;

                        case "parcheggio" or "parcheggi":
                            luogoTrovato = "";
                            foreach (var business in data?.ResourceSets?[0]?.Resources?[0]?.BusinessesAtLocation)
                            {
                                for (int i = 0; i < business?.BusinessInfo?.OtherTypes?.Count; i++)
                                {
                                    if (parcheggi.Contains(business?.BusinessInfo?.OtherTypes?[i]))
                                    {
                                        if (primo == true)
                                        {
                                            await synthesizer.SpeakTextAsync($"Parcheggi a {luogo}");
                                            primo = false;
                                        }
                                        if (business?.BusinessInfo?.EntityName != luogoTrovato)
                                        {
                                            luogoTrovato = business.BusinessInfo.EntityName;
                                            await synthesizer.SpeakTextAsync(luogoTrovato);
                                            Debug.WriteLine(luogoTrovato);
                                        }
                                    }
                                }
                            }
                            break;

                        default:
                            await synthesizer.SpeakTextAsync($"non ci sono {tipoLuogo} a {luogo}");
                            break;
                    }


                    if (primo == true)
                        await synthesizer.SpeakTextAsync($"non ho trovato nessun ristorante a {luogo}");
                }
                else
                {
                    LocalRecognition? data = await response.Content.ReadFromJsonAsync<LocalRecognition>();
                    await synthesizer.SpeakTextAsync($"a {luogo} troviamo:");
                    foreach (var item in data.ResourceSets[0].Resources[0].BusinessesAtLocation)
                    {
                        Debug.WriteLine(item.BusinessInfo.EntityName + ", Tipo: " + item.BusinessInfo.Type);
                        await synthesizer.SpeakTextAsync(item.BusinessInfo.EntityName);
                        await synthesizer.SpeakTextAsync("Tipo:");
                        await synthesizer.SpeakTextAsync(item.BusinessInfo.Type);
                    }
                }
            }
        }
        #endregion

        #region WIKIPEDIA
        public static async Task WikiSearch(string? ricerca, string? sezione, SpeechSynthesizer synthesizer)
        {
            string? key = await SearchKeyText(ricerca);
            string? testoDaDire = null, sezioneTrovata = null;
            if (key != null)
            {
                if (sezione == null)
                    testoDaDire = await ExtractSummaryByKey(key, null);
                else
                {
                    sezioneTrovata = await SearchSections(key, sezione);
                    if (sezioneTrovata != null)
                        testoDaDire = await ExtractSummaryByKey(key, sezioneTrovata);
                }
            }

            Debug.WriteLine("Testo: " + testoDaDire);

            if (testoDaDire != null)
            {
                await synthesizer.SpeakTextAsync(testoDaDire);
            }
            else
            {
                if (sezione == null && sezioneTrovata == null)
                    await synthesizer.SpeakTextAsync($"Non ho trovato {ricerca} su Wikipedia");
                else if (sezione != null && sezioneTrovata == null)
                    await synthesizer.SpeakTextAsync($"Non ho trovato la sezione {sezione} nella pagina {ricerca} su Wikipedia");
            }

        }
        static async Task<string?> SearchKeyText(string argument)
        {
            string argumentClean = HttpUtility.UrlEncode(argument);
            string keyUrl = $"https://it.wikipedia.org/w/rest.php/v1/search/page?q={argumentClean}&limit=1";
            Debug.WriteLine("Cerco la key: " + keyUrl);
            // recupero la chiave di ricerca con il parsing del dom
            var response = await client.GetAsync(keyUrl);
            if (response.IsSuccessStatusCode)
            {
                KeyModel? model = await response.Content.ReadFromJsonAsync<KeyModel>();
                if (model != null)
                {
                    string? keySearch = null;
                    try
                    {
                        keySearch = model.Pages[0].Key;
                    }
                    catch (Exception)
                    {
                        return null;
                    }

                    return keySearch;
                }
            }
            return null;
        }
        static async Task<string?> ExtractSummaryByKey(string keySearch, string sezione)
        {
            string? wikiUrl = null;
            if (sezione == null)
            {
                wikiUrl = $"https://it.wikipedia.org/w/api.php?format=json&action=query&prop=extracts&exintro&explaintext&exsectionformat=plain&redirects=1&titles={keySearch}";
                Debug.WriteLine("Link finale: " + wikiUrl);
                string wikiSummaryJSON = await client.GetStringAsync(wikiUrl);
                using JsonDocument document = JsonDocument.Parse(wikiSummaryJSON);
                JsonElement root = document.RootElement;
                JsonElement query = root.GetProperty("query");
                JsonElement pages = query.GetProperty("pages");
                JsonElement.ObjectEnumerator enumerator = pages.EnumerateObject();
                if (enumerator.MoveNext())
                {
                    JsonElement target = enumerator.Current.Value;
                    if (target.TryGetProperty("extract", out JsonElement extract))
                    {
                        return extract.GetString() ?? string.Empty;
                    }
                }
                return null;
            }
            else
            {
                wikiUrl = $"https://it.wikipedia.org/w/api.php?action=parse&format=json&page={keySearch}&prop=wikitext&section={sezione}&disabletoc=1";
                HttpResponseMessage response = await client.GetAsync(wikiUrl);
                Debug.WriteLine("Link finale: " + wikiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var section = await response.Content.ReadFromJsonAsync<SectionSummary>();
                    string? riassunto = section?.Parse?.WikiText?.Testo;
                    if (riassunto != null)
                    {
                        return WikitextHelper.WikiTextToReadableTextNoSpace(riassunto);
                    }
                }
                return null;
            }

        }
        static async Task<string?> SearchSections(string? key, string? sezione)
        {
            string urlSection = $"https://it.wikipedia.org/w/api.php?action=parse&format=json&page={key}&prop=sections&disabletoc=1";
            Debug.WriteLine("Cerco sezione: " + urlSection);
            var response = await client.GetAsync(urlSection);
            // parso le sezioni e recupero la key e l'indice di sezione
            if (response.IsSuccessStatusCode)
            {
                SectionModel? sectionModel = await response.Content.ReadFromJsonAsync<SectionModel>();
                if (sectionModel != null)
                {
                    List<Section1> sections = sectionModel.Parse.Sections;
                    foreach (Section1 section in sections)
                    {
                        if (section.Line.ToLower() == sezione)
                        {
                            return section.Index;
                        }
                    }
                }
            }
            return null;
        }
        #endregion

        public static async Task<(double? lat, double? lon)?> TrovaCoordinate(string? città)
        {
            string? cittaCod = HttpUtility.UrlEncode(città);
            string urlCoordinate = $"https://geocoding-api.open-meteo.com/v1/search?name={cittaCod}&count=1&language=it";
            try
            {
                HttpResponseMessage response = await client.GetAsync($"{urlCoordinate}");
                if (response.IsSuccessStatusCode)
                {
                    //await Console.Out.WriteLineAsync(await response.Content.ReadAsStringAsync());
                    GeoCoding? geoCoding = await response.Content.ReadFromJsonAsync<GeoCoding>();
                    if (geoCoding != null && geoCoding.Results?.Count > 0)
                    {
                        return (geoCoding.Results[0].Latitude, geoCoding.Results[0].Longitude);
                    }
                }
                return null;
            }
            catch (Exception)
            {

                Console.WriteLine("Errore");
            }
            return null;
        }

        private void UpdateUI(String message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                RecognitionText.Text = message;
            });
        }
    }
}
