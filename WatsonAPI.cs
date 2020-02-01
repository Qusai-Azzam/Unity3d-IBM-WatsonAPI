
#pragma warning disable 0649

using UnityEngine;
using UnityEngine.UI;
using IBM.Cloud.SDK;
using System.Collections;
using IBM.Cloud.SDK.Utilities;
using IBM.Cloud.SDK.DataTypes;
using IBM.Watson.SpeechToText.V1;
using IBM.Watson.TextToSpeech.V1;
using IBM.Cloud.SDK.Authentication.Iam;
using IBM.Watson.Assistant.V1;
using IBM.Watson.Assistant.V1.Model;

public class WatsonAPI : MonoBehaviour
{
    
    #region Text To Speech Service Variables
    [Header("Text To Speech IAM APIKey")]
    [Tooltip("Text To Speech  IAM apikey.")]
    [SerializeField]
    private string TTSIamApikey;

    [Tooltip("Text To Speech service URL ")]
    [SerializeField]
    private string TTSServiceUrl;

    private TextToSpeechService TTSService;

    private string allisionVoice = "en-US_AllisonVoice";
    private string synthesizeMimeType = "audio/wav";
    #endregion

    #region Speech To Text Service Variables

    [Header("Speech to Text Service")]

    [Tooltip("Speak To Text IAM Authentication")]
    [SerializeField]
    private string STTIamApikey;

    [Tooltip("Speak To Text Service URL")]
    [SerializeField]
    private string STTServiceUrl;

    [Tooltip("Text field to display the results of streaming.")]
    public Text ResultsField;


    private string _recognizeModel;
    private int _recordingRoutine = 0;
    private string _microphoneID = null;
    private AudioClip _recording = null;
    private int _recordingBufferSize = 1;
    private int _recordingHZ = 22050;

    private SpeechToTextService STTservice;

    #endregion

    #region Assisatnt Service Variables

    [Header("Assistant Service ")]
    [Tooltip("Assistant Service IAM Authentication")]
    [SerializeField]
    private string ASSIamApikey;
    [Tooltip("Assistant Service URL")]
    [SerializeField]
    private string ASServiceUrl;

    [Tooltip("WorkspaceID")]
    [SerializeField]
    private string workspaceId;

    private AssistantService ASService;

    #endregion

    private string TTS_content = "";
    public bool play = false;

    private float wait = 0;

    private bool check = false;

    void Start()
    {
        LogSystem.InstallDefaultReactors();

        // Create TTS Service
        Runnable.Run(CreateTTSService());

        // Create Assistant Service
        Runnable.Run(CreateASService());

        // Create STT Service
        Runnable.Run(CreateSSTService());
    }

    private void Update()
    {
        if (play)
        {
            Debug.Log(" play=true in Update");
            play = false;
            Active = false;
            GetTTS();
        }

        if (check)
        {
            wait -= Time.deltaTime; //reverse count
        }

        if ((wait < 0f) && (check))
        {
            //check that clip is not playing      
            Debug.Log("Speech has finished");
            check = false;

            //Now let's start listening again.....
            if (!Active)
            {
                Active = true;
            }
            StartRecording();
        }

    }

    private void GetTTS()
    {
        //  Synthesize
        Log.Debug("WatsonTTS", "Attempting synthesize.");
        byte[] synthesizeResponse = null;
        AudioClip clip = null;

        TTSService.Synthesize(
            callback: (DetailedResponse<byte[]> response, IBMError error) =>
            {
                synthesizeResponse = response.Result;
                clip = WaveFile.ParseWAV("myClip", synthesizeResponse);
                wait = clip.length;
                check = true;
                PlayClip(clip);
            },
            text: TTS_content,
            voice: allisionVoice,
            accept: synthesizeMimeType
        );

    }
    private void PlayClip(AudioClip clip)
    {
        if (Application.isPlaying && clip != null)
        {
            GameObject audioObject = new GameObject("AudioObject");
            AudioSource source = audioObject.AddComponent<AudioSource>();
            source.spatialBlend = 0.0f;
            source.loop = false;
            source.clip = clip;
            source.Play();
            GameObject.Destroy(audioObject, clip.length);
        }
    }
    private IEnumerator CreateASService()
    {
        IamAuthenticator authenticator = new IamAuthenticator(apikey: ASSIamApikey);

        //  Wait for tokendata
        while (!authenticator.CanAuthenticate())
            yield return null;

        ASService = new AssistantService("2019-02-18", authenticator);
        ASService.SetServiceUrl(ASServiceUrl);

        Debug.Log(" Assistant Service Created ...");
    }

    private void OnReceiveMessage(DetailedResponse<MessageResponse> response, IBMError error)
    {
        if (response.Result != null)
        {
            if (response.Result.Output.Text.Count > 0)
            {
                for (int i = 0; i < response.Result.Output.Text.Count; i++)
                {
                    Debug.Log(string.Format(" Response {0}:{1} ", i, response.Result.Output.Text[i]));
                }
                TTS_content = response.Result.Output.Text[0];
                ResultsField.text = TTS_content;

                //trigger the Update to PLAY the TTS message
                play = true;
            }
            else
            {
                check = true;
                wait = 2f;
            }
        }
        else
        {

            TTS_content = "";
            check = true;
            wait = 2f;
        }

    }

    private IEnumerator CreateTTSService()
    {
        if (string.IsNullOrEmpty(TTSIamApikey))
        {
            throw new IBMException("Please add TTS IAM ApiKey to the Iam Apikey field in the inspector.");
        }

        IamAuthenticator authenticator = new IamAuthenticator(apikey: TTSIamApikey);

        while (!authenticator.CanAuthenticate())
        {
            yield return null;
        }

        TTSService = new TextToSpeechService(authenticator);

        if (!string.IsNullOrEmpty(TTSServiceUrl))
        {
            TTSService.SetServiceUrl(TTSServiceUrl);
        }
        Debug.Log(" Text To Speech Service Created ...");
    }

    public bool Active
    {
        get { return STTservice.IsListening; }
        set
        {
            if (value && !STTservice.IsListening)
            {
                STTservice.RecognizeModel = (string.IsNullOrEmpty(_recognizeModel) ? "en-US_BroadbandModel" : _recognizeModel);
                STTservice.DetectSilence = true;
                STTservice.EnableWordConfidence = true;
                STTservice.EnableTimestamps = true;
                STTservice.SilenceThreshold = 0.01f;
                STTservice.MaxAlternatives = 1;
                STTservice.EnableInterimResults = true;
                STTservice.OnError = OnError;
                STTservice.InactivityTimeout = -1;
                STTservice.ProfanityFilter = false;
                STTservice.SmartFormatting = true;
                STTservice.SpeakerLabels = false;
                STTservice.WordAlternativesThreshold = null;
                STTservice.EndOfPhraseSilenceTime = 2;

                STTservice.StartListening(OnRecognize, OnRecognizeSpeaker);
            }
            else if (!value && STTservice.IsListening)
            {
                STTservice.StopListening();
            }
        }
    }

    private void OnError(string error)
    {
        Active = false;
        Log.Debug("SST OnError()", "Error! {0}", error);
    }

    private void OnRecognize(SpeechRecognitionEvent result)
    {
        if (result != null && result.results.Length > 0)
        {
            foreach (var res in result.results)
            {
                foreach (var alt in res.alternatives)
                {
                    string text = string.Format("{0} ({1}, {2:0.00})\n", alt.transcript, res.final ? "Final" : "Interim", alt.confidence);

                    if (res.final)
                    {
                        ResultsField.text = text;
                        string _conversationString = alt.transcript;
                        //We can now call the CONV service?
                        Log.Debug("STT.OnSTTRecognize()", _conversationString);

                        Active = false;  //Stop Microphone from listening

                        //  Message
                        MessageInput messageRequest = new MessageInput()
                        {
                            Text = _conversationString
                        };
                        ASService.Message(OnReceiveMessage, workspaceId: workspaceId, input: messageRequest);

                    }

                }

            }
        }
    }

    private void OnRecognizeSpeaker(SpeakerRecognitionEvent result)
    {
        if (result != null)
        {
            foreach (SpeakerLabelsResult labelResult in result.speaker_labels)
            {
                Log.Debug("ExampleStreaming.OnRecognizeSpeaker()", string.Format("speaker result: {0} | confidence: {3} | from: {1} | to: {2}", labelResult.speaker, labelResult.from, labelResult.to, labelResult.confidence));
            }
        }
    }

    private void StartRecording()
    {
        if (_recordingRoutine == 0)
        {
            UnityObjectUtil.StartDestroyQueue();
            _recordingRoutine = Runnable.Run(RecordingHandler());
        }
    }
    private IEnumerator RecordingHandler()
    {
        Log.Debug("ExampleStreaming.RecordingHandler()", "devices: {0}", Microphone.devices);
        _recording = Microphone.Start(_microphoneID, true, _recordingBufferSize, _recordingHZ);
        yield return null;      // let _recordingRoutine get set..

        if (_recording == null)
        {
            StopRecording();
            yield break;
        }

        bool bFirstBlock = true;
        int midPoint = _recording.samples / 2;
        float[] samples = null;

        while (_recordingRoutine != 0 && _recording != null)
        {
            int writePos = Microphone.GetPosition(_microphoneID);
            if (writePos > _recording.samples || !Microphone.IsRecording(_microphoneID))
            {
                Log.Error("ExampleStreaming.RecordingHandler()", "Microphone disconnected.");

                StopRecording();
                yield break;
            }

            if ((bFirstBlock && writePos >= midPoint)
              || (!bFirstBlock && writePos < midPoint))
            {
                // front block is recorded, make a RecordClip and pass it onto our callback.
                samples = new float[midPoint];
                _recording.GetData(samples, bFirstBlock ? 0 : midPoint);

                AudioData record = new AudioData();
                record.MaxLevel = Mathf.Max(Mathf.Abs(Mathf.Min(samples)), Mathf.Max(samples));
                record.Clip = AudioClip.Create("Recording", midPoint, _recording.channels, _recordingHZ, false);
                record.Clip.SetData(samples, 0);

                STTservice.OnListen(record);

                bFirstBlock = !bFirstBlock;
            }
            else
            {
                // calculate the number of samples remaining until we ready for a block of audio, 
                // and wait that amount of time it will take to record.
                int remaining = bFirstBlock ? (midPoint - writePos) : (_recording.samples - writePos);
                float timeRemaining = (float)remaining / (float)_recordingHZ;

                yield return new WaitForSeconds(timeRemaining);
            }
        }
        yield break;
    }

    private void StopRecording()
    {
        if (_recordingRoutine != 0)
        {
            Microphone.End(_microphoneID);
            Runnable.Stop(_recordingRoutine);
            _recordingRoutine = 0;
        }
    }
    private IEnumerator CreateSSTService()
    {
        if (string.IsNullOrEmpty(STTIamApikey))
        {
            throw new IBMException("Plesae provide IAM ApiKey for the service.");
        }

        IamAuthenticator authenticator = new IamAuthenticator(apikey: STTIamApikey);

        //  Wait for tokendata
        while (!authenticator.CanAuthenticate())
            yield return null;

        STTservice = new SpeechToTextService(authenticator);

        if (!string.IsNullOrEmpty(STTServiceUrl))
        {
            STTservice.SetServiceUrl(STTServiceUrl);
        }
        STTservice.StreamMultipart = true;
        Debug.Log(" Speak To Text Service Created ...");

        Active = true;

        StartRecording();

    }

}
