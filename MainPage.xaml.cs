/*
    Copyright(c) Microsoft Open Technologies, Inc. All rights reserved.

    The MIT License(MIT)

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files(the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions :

    The above copyright notice and this permission notice shall be included in
    all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
    THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Devices;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.Graphics.Imaging;
using Windows.Devices.Enumeration;
using Windows.Storage.Pickers;
using Windows.Media.FaceAnalysis;
using Windows.ApplicationModel;
using Windows.Foundation.Metadata;
using Windows.Graphics.Display;
using Windows.Phone.UI.Input;
using Windows.Storage.FileProperties;
using Windows.System.Display;
using Windows.Web.Http;
using Windows.Web.Http.Filters;
using Windows.Web.Http.Headers;
using Windows.ApplicationModel.Resources.Core;
using Windows.Media.SpeechSynthesis;
using Windows.Data.Json;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;


//Include the package for face api
// Look into whether the source control is needed for this part

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace fr_newer
{
    
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private readonly FaceServiceClient faceServiceClient = new FaceServiceClient("7703c42821cf4256942c61faf78062ce");

        //  private FaceDetectionEffect _faceDetectionEffect;
        private StorageFolder captureFolder = null;

        private MediaCapture mediaCapture;
        private StorageFile photoFile;
        private StorageFile recordStorageFile;
        private StorageFile audioFile;
        private readonly string PHOTO_FILE_NAME = "photo.jpg";
        private readonly string VIDEO_FILE_NAME = "video.mp4";
        private readonly string AUDIO_FILE_NAME = "audio.mp3";
        private IMediaEncodingProperties _previewProperties;
        private bool isPreviewing;
        private bool isRecording;

        // Information about the camera device
        private static object _lock = new object();
        private int faceNumber = 0;
        private static DateTime preTime = DateTime.MinValue, nowTime = DateTime.Now;
        private FaceDetectionEffect _faceDetectionEffect;

        #region HELPER_FUNCTIONS
        enum Action
        {
            ENABLE,
            DISABLE
        }

        /// <summary>
        /// Helper function to enable or disable Initialization buttons
        /// </summary>
        /// <param name="action">enum Action</param>
        private void SetInitButtonVisibility(Action action)
        {
            if (action == Action.ENABLE)
            {
                video_init.IsEnabled = true;
            }
            else
            {
                video_init.IsEnabled = false;
            }
        }

        private void UpdateCaptureControls()
        {
            //Hide the face detection canvas and clear it
            captureImage.Visibility = (_faceDetectionEffect == null || !_faceDetectionEffect.Enabled) ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        public MainPage()
        {
            this.InitializeComponent();

            SetInitButtonVisibility(Action.ENABLE);

            isPreviewing = false;
        }

        private async void Cleanup()
        {
            if (mediaCapture != null)
            {
                // Cleanup MediaCapture object
                if (isPreviewing)
                {
                    await mediaCapture.StopPreviewAsync();
                    captureImage.Children.Clear();//captureImage.Children.Clear();//captureImage.Source = null;
                    isPreviewing = false;
                }
                mediaCapture.Dispose();
                mediaCapture = null;
            }
            SetInitButtonVisibility(Action.ENABLE);
        }

        private async Task CleanUpFaceDetectionEffectAsync()
        {
            //Disable Detection
            _faceDetectionEffect.Enabled = false;

            // Unregister the event handler
            _faceDetectionEffect.FaceDetected -= FaceDetectionEffect_FaceDetected;

            // Remove the effect from the preview stream
            await mediaCapture.ClearEffectsAsync(MediaStreamType.VideoPreview);

            //Clear the member variable that held the effect instance
            _faceDetectionEffect = null;
        }


        /// <summary>
        /// 'Initialize Audio and Video' button action function
        /// Dispose existing MediaCapture object and set it up for audio and video
        /// Enable or disable appropriate buttons
        /// - DISABLE 'Initialize Audio and Video' 
        /// - DISABLE 'Start Audio Record'
        /// - ENABLE 'Initialize Audio Only'
        /// - ENABLE 'Start Video Record'
        /// - ENABLE 'Take Photo'
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void initVideo_Click(object sender, RoutedEventArgs e)
        {
            // Disable all buttons until initialization completes

            SetInitButtonVisibility(Action.DISABLE);
            try
            {
                if (mediaCapture != null)
                {
                    // Cleanup MediaCapture object
                    if (isPreviewing)
                    {
                        await mediaCapture.StopPreviewAsync();
                        //captureImage.Source = null;
                        captureImage.Children.Clear();//captureImage.Children.Clear();//
                        isPreviewing = false;
                    }
                    await CleanUpFaceDetectionEffectAsync();
                    mediaCapture.Dispose();
                    mediaCapture = null;
                }

                status.Text = "Initializing camera to capture audio and video...";
                
                // Use default initialization
                mediaCapture = new MediaCapture();
                await mediaCapture.InitializeAsync();

                // Set callbacks for failure and recording limit exceeded
                //status.Text = "Device successfully initialized for video recording!";
                mediaCapture.Failed += new MediaCaptureFailedEventHandler(mediaCapture_Failed);
                mediaCapture.RecordLimitationExceeded += new Windows.Media.Capture.RecordLimitationExceededEventHandler(MediaCapture_RecordLimitationExceeded);

                // Start Preview                
                previewElement.Source = mediaCapture;
                await mediaCapture.StartPreviewAsync();
                isPreviewing = true;
                status.Text = "Camera preview succeeded";

                // _faceDetectionEffect.Enabled = true;

                if (_faceDetectionEffect == null || !_faceDetectionEffect.Enabled)
                {
                    // clear any rectangles that may have been left over from a previous instance of the effect
                    captureImage.Children.Clear();//captureImage.Children.Clear();
                    await CreateFaceDetectionEffectAsync();
                }
                else
                {
                    await CleanUpFaceDetectionEffectAsync();
                }

                UpdateCaptureControls();

            }
            catch (Exception ex)
            {
                Debug.WriteLine("Unable to Initialize Camera for audio/video mode" + ex.Message);
                status.Text = "Unable to initialize camera for audio/video mode: " + ex.Message;
            }
        }

        private async Task CreateFaceDetectionEffectAsync()
        {
            //Create the definition, which will contain some initialization settings
            var definition = new FaceDetectionEffectDefinition();

            //To ensure preview smoothness, do not delay incoming samples
            definition.SynchronousDetectionEnabled = false;

            //In this scenario, choose detection speed over accuracy
            definition.DetectionMode = FaceDetectionMode.HighPerformance;

            //Add the effect to the preview stream
            _faceDetectionEffect = (FaceDetectionEffect)await mediaCapture.AddVideoEffectAsync(definition, MediaStreamType.VideoPreview);

            //Register for face detection events
            _faceDetectionEffect.FaceDetected += FaceDetectionEffect_FaceDetected;

            // Choose the shortest interval between detection events
            _faceDetectionEffect.DesiredDetectionInterval = TimeSpan.FromMilliseconds(33);

            // Start detecting faces
            _faceDetectionEffect.Enabled = true;

            //status.Text = "The CreateFaceDetectionEffectAsync has been done...";
            Debug.WriteLine("The CreateFaceDetectionEffectAsync has been done...");
        }

        private async void FaceDetectionEffect_FaceDetected(FaceDetectionEffect sender, FaceDetectedEventArgs args)
        {
            Debug.WriteLine("Face Number: {0}", args.ResultFrame.DetectedFaces.Count);

            //Ask the UI thread to render the face bounding boxes
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => HighlightDetectedFaces(args.ResultFrame.DetectedFaces));

            try
            {
                //if(args.ResultFrame.DetectedFaces.Count > faceNumber)
                //{
                //    faceNumber = args.ResultFrame.DetectedFaces.Count;
                //    //await SendPhotoAsync();
                //}
                //else
                //{
                //    faceNumber = args.ResultFrame.DetectedFaces.Count;
                //}
                faceNumber = args.ResultFrame.DetectedFaces.Count;
            }

            catch (Exception ex)
            {
                Debug.WriteLine("Exception when sending a photo: {0}", ex.ToString());
            }
            //status.Text = "The number of faces is " + faceNumber;
           // Debug.WriteLine("The number of faces is" + faceNumber); 
        }
        
        /// <summary>
        /// Iterates over all detected faces, creating and adding Rectangles to the FacesCanvas as face bounding boxes
        /// </summary>
        /// <param name="faces">The list of detected faces from the FaceDetected event of the effect</param>
        private void HighlightDetectedFaces(IReadOnlyList<DetectedFace> faces)
        {
            // Remove any leftover face rectangles from previous events
            captureImage.Children.Clear();// captureImage.Children.Clear();

            // For each detected face
            for (int i = 0; i < faces.Count; i++)
            {
                // Face coordinate units are preview resolution pixels, which can be a different scale from our display resolution;
                // a conversion may be necessary
                Rectangle faceBoundingBox = ConvertPreviewToUiRectangle(faces[i].FaceBox);

                //set bounding box stroke properties
                faceBoundingBox.StrokeThickness = 2;

                //Highlight the first face in the set
                faceBoundingBox.Stroke = (i == 0 ? new SolidColorBrush(Colors.Blue) : new SolidColorBrush(Colors.DeepSkyBlue));

                // Add grid to canvas containing all face UI in the set
                captureImage.Children.Add(faceBoundingBox);//captureImage.Children.Add(faceBoundingBox);

               // status.Text = "Faces should be highighted...";
                Debug.WriteLine("The faces should have been highlisted by now");
            }
        }


        /// <summary>
        /// Takes face information defined in preview coordinates and returns one in UI coordinates, taking
        /// into account the position and size of the preview control.
        /// </summary>
        /// <param name="faceBoxInPreviewCoordinates">Face coordinates as retried from the FaceBox property of a DetectedFace, in preview coordinates.</param>
        /// <returns>Rectangle in UI (CaptureElement) coordinates, to be used in a Canvas control.</returns>
        private Rectangle ConvertPreviewToUiRectangle(BitmapBounds faceBoxInPreviewCoordinates)
        {
            var result = new Rectangle();
            var previewStream = _previewProperties as VideoEncodingProperties;

            // If there is no available info about the preview, return empty rectable, as rescaling to the screen 
            // coordinates will be impossible 
            if (previewStream == null)
                return result;

            // Similarly, if any of the dimensions are zero (which happens in an error case, return an empty rectangle
            if (previewStream.Width == 0 || previewStream.Height == 0)
                return result;

            double streamWidth = previewStream.Width;
            double streamHeight = previewStream.Height;

            // Get the rectangle that is occupied by the actual video feed
            var previewInUi = GetPreviewStreamRectInControl(previewStream, previewElement);

            // Scale the width and height from the preview stream coordinates to window coordinates
            result.Width = (faceBoxInPreviewCoordinates.Width / streamWidth) * previewInUi.Width;
            result.Height = (faceBoxInPreviewCoordinates.Height / streamHeight) * previewInUi.Height;

            //Scale the X and Y coords from preview stream coords to window coords
            var x = (faceBoxInPreviewCoordinates.X / streamWidth) * previewInUi.Width;
            var y = (faceBoxInPreviewCoordinates.Y / streamHeight) * previewInUi.Height;
            Canvas.SetLeft(result, x);
            Canvas.SetTop(result, y);

            return result;
        }
        
        /// <summary>
        /// Calculates the size and location of the rectangle that contains the preview stream within the preview control, when the scaling mode is Uniform
        /// </summary>
        /// <param name="previewResolution">The resolution at which the preview is running</param>
        /// <param name="previewControl">The control that is displaying the preview using Uniform as the scaling mode</param>
        /// <returns></returns>
        public Rect GetPreviewStreamRectInControl(VideoEncodingProperties previewResolution, CaptureElement previewElement)
        {
            var result = new Rect();

            // In case this function is called before everything is initialized correctly, return an empty result
            if (previewElement == null || previewElement.ActualHeight < 1 || previewElement.ActualWidth < 1 ||
                previewResolution == null || previewResolution.Height == 0 || previewResolution.Width == 0)
            {
                return result;
            }

            var streamWidth = previewResolution.Width;
            var streamHeight = previewResolution.Height;

            // Start by assuming the preview display area in control spans the entire width and height both
            result.Width = previewElement.ActualWidth;
            result.Height = previewElement.ActualHeight;

            // If UI is "wider" than preview, letterboxing will be on the sides
            if ((previewElement.ActualWidth / previewElement.ActualHeight > streamHeight / (double)streamHeight))
            {
                var scale = previewElement.ActualHeight / streamHeight;
                var scaledWidth = streamWidth * scale;

                result.X = (previewElement.ActualWidth - scaledWidth) / 2.0;
                result.Width = scaledWidth;
            }
            else // if the preview string is wider than the UI, so letterboxing will be on the top+bottom
            {
                var scale = previewElement.ActualWidth / streamWidth;
                var scaledHeight = streamHeight * scale;

                result.Y = (previewElement.ActualHeight - scaledHeight) / 2.0;
                result.Height = scaledHeight;
            }
            return result;
        }


        private void cleanup_Click(object sender, RoutedEventArgs e)
        {
            SetInitButtonVisibility(Action.DISABLE);
            Cleanup();
        }

        /// <summary>
        /// Callback function for any failures in MediaCapture operations
        /// </summary>
        /// <param name="currentCaptureObject"></param>
        /// <param name="currentFailure"></param>
        private async void mediaCapture_Failed(MediaCapture currentCaptureObject, MediaCaptureFailedEventArgs currentFailure)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    //status.Text = "MediaCaptureFailed: " + currentFailure.Message;

                    if (isRecording)
                    {
                        await mediaCapture.StopRecordAsync();
                        //status.Text += "\n Recording Stopped";
                    }
                }
                catch (Exception)
                {
                }
                finally
                {
                    SetInitButtonVisibility(Action.DISABLE);
                    //status.Text += "\nCheck if camera is diconnected. Try re-launching the app";
                }
            });
        }

        /// <summary>
        /// Stops recording a video
        /// </summary>
        /// <returns></returns>
        private async Task StopRecordingAsync()
        {
            try
            {
                Debug.WriteLine("Stopping recording...");

                isRecording = false;
                await mediaCapture.StopRecordAsync();

                Debug.WriteLine("Stopped recording!");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception when stopping video recording: {0}", ex.ToString());
            }
        }

        /// <summary>
        /// Callback function if Recording Limit Exceeded
        /// </summary>
        private async void MediaCapture_RecordLimitationExceeded(MediaCapture sender)
        {
            // This is a notification that recording has to stop, and the app is expected to finalize the recording

            await StopRecordingAsync();

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateCaptureControls());
        }

    }
}
