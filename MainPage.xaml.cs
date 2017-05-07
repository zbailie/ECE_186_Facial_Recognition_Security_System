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
using Windows.Devices.Gpio;
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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace fr_newest
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private readonly FaceServiceClient faceServiceClient = new FaceServiceClient("7703c42821cf4256942c61faf78062ce");

        //  private FaceDetectionEffect _faceDetectionEffect;
        private StorageFolder captureFolder = null;

        private SoftwareBitmap bitmapSource;
        private const string personGroupId = "family3";
        private const string guestId = "guest";
        private MediaCapture mediaCapture;
        private StorageFile photoFile;
        private StorageFile GuestFile;
        private StorageFile dir;
        private readonly string PHOTO_FILE_NAME = "photo.jpg";
        private IMediaEncodingProperties _previewProperties;
        private bool isPreviewing;
        private bool isRecording;
        
//GPIO stuff

        private GpioPin pin;
        private const int LED_PIN = 12;
        private GpioPinValue pinValue;
        private DispatcherTimer timer;

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

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(10);
            //timer.Tick += IdentifyFace_Click;
            InitGPIO(); 
        }

        private void InitGPIO()
        {
            var gpio = GpioController.GetDefault();

            //show an error if there is not GPIO contorller
            if (gpio == null)
            {
                pin = null;
                status.Text = "There is not GPIO controller on this device";
                return;
            }
            pin = gpio.OpenPin(LED_PIN);
            pinValue = GpioPinValue.Low;
            pin.Write(pinValue);
            pin.SetDriveMode(GpioPinDriveMode.Output);

            status.Text = "GPIO Pin initialized correctly";

        }

        private async Task Timer_Tick()//object sender, object e)
        {
            //timer = new DispatcherTimer();
            //timer.Interval = TimeSpan.FromSeconds(10);
            pinValue = GpioPinValue.High;
            pin.Write(pinValue);
            await Task.Delay(10000);
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

            var gpio = GpioController.GetDefault();
            pin = gpio.OpenPin(LED_PIN);
            pinValue = GpioPinValue.High;
            pin.Write(pinValue);
            pin.SetDriveMode(GpioPinDriveMode.Output);

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

                //if (_faceDetectionEffect == null || !_faceDetectionEffect.Enabled)
                //{
                //    // clear any rectangles that may have been left over from a previous instance of the effect
                //    captureImage.Children.Clear();//captureImage.Children.Clear();
                //    await CreateFaceDetectionEffectAsync();
                //}
                //else
                //{
                //    await CleanUpFaceDetectionEffectAsync();
                //}

                //UpdateCaptureControls();

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


        // -----------------------------------------------------------------------------
        // --- This part is for the face identification/verification
        private async void takephoto_Click(object sender, RoutedEventArgs e)
        {
            var uniquefilename = string.Format(@"{0}.jpg", DateTime.Now.Ticks);
            photoFile = await KnownFolders.PicturesLibrary.CreateFileAsync(uniquefilename);//PHOTO_FILE_NAME, CreationCollisionOption.GenerateUniqueName);
            ImageEncodingProperties imageProperties = ImageEncodingProperties.CreateJpeg();
            await mediaCapture.CapturePhotoToStorageFileAsync(imageProperties, photoFile);

            var stream = await photoFile.OpenAsync(FileAccessMode.Read);
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

            BitmapTransform transform = new BitmapTransform();
            const float sourceImageHeightLimit = 1280;

            status.Text = "Photo created";

            if (decoder.PixelHeight > sourceImageHeightLimit)
            {
                float scalingFactor = (float)sourceImageHeightLimit / (float)decoder.PixelHeight;
                transform.ScaledWidth = (uint)Math.Floor(decoder.PixelWidth * scalingFactor);
                transform.ScaledHeight = (uint)Math.Floor(decoder.PixelHeight * scalingFactor);
            }
            bitmapSource =
                await decoder.GetSoftwareBitmapAsync(decoder.BitmapPixelFormat, BitmapAlphaMode.Premultiplied, transform,
                ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);

            ImageBrush brush = new ImageBrush();
            SoftwareBitmapSource BitMapSource = new SoftwareBitmapSource();
            await BitMapSource.SetBitmapAsync(bitmapSource);
            brush.ImageSource = BitMapSource;
            brush.Stretch = Stretch.Uniform;
            captureImage.Background = brush;

            status.Text = "Image loaded in the following location: " + photoFile.Path;

            //face_init.IsEnabled = true;
            //identify_init.IsEnabled = false;
            //traingroup.IsEnabled = false;
            //detect_init.IsEnabled = false; //generate the person group
            //traingroup.IsEnabled = false;
            //cleanup.IsEnabled = true; 

        }

        private async void initface_Click(object sender, RoutedEventArgs e)
        {
            //try
            //{
            FaceRectangle[] faceRects = await UploadAndDetectFaces();
            status.Text = $"Detection finished. {faceRects.Length} face(s) detected";

            //identify_init.IsEnabled = false;
            //traingroup.IsEnabled = false;
            //detect_init.IsEnabled = true; //generate the person group
            //traingroup.IsEnabled = false;
            //cleanup.IsEnabled = true;

            //if (faceRects.Length > 0)
            //{
            //    //MarkFaces(faceRects);
            //    GeneratePersonGroup();
            //}
            //}
            //catch (Exception ex)
            //{
            //    status.Text = $"Status: {ex.Message}";
            //}
        }

        private async Task<FaceRectangle[]> UploadAndDetectFaces()
        {
            try
            {
                status.Text = "Detecting Faces.....";
                using (Stream imageFileStream = await photoFile.OpenStreamForReadAsync())
                {
                    //status.Text = "Entered facial recognition"; 
                    var faces = await faceServiceClient.DetectAsync(imageFileStream);
                    var faceRects = faces.Select(face => face.FaceRectangle);
                    return faceRects.ToArray();
                }
            }
            catch (Exception ex)
            {
                status.Text = $"Status: {ex.Message}";
                return new FaceRectangle[0];
            }
        }

        private void MarkFaces(FaceRectangle[] faceRects)
        {
            SolidColorBrush lineBrush = new SolidColorBrush(Colors.Coral);
            SolidColorBrush fillBrush = new SolidColorBrush(Colors.Transparent);
            double lineThickness = 3.0;

            double dpi = bitmapSource.DpiX;
            double resizeFactor = 96 / dpi;
            //status.Text = "In the markfaces function";
            if (faceRects != null)
            {
                double widthScale = bitmapSource.PixelWidth / captureImage.ActualWidth;
                double heightScale = bitmapSource.PixelHeight / captureImage.ActualHeight;

                foreach (var faceRectangle in faceRects)
                {
                    Rectangle box = new Rectangle
                    {
                        Width = (uint)(faceRectangle.Width / widthScale) - faceRectangle.Width,
                        Height = (uint)(faceRectangle.Height / heightScale) - faceRectangle.Height,
                        Fill = fillBrush,
                        Stroke = lineBrush,
                        StrokeThickness = lineThickness,
                        Margin = new Thickness((uint)(faceRectangle.Left / widthScale) + faceRectangle.Width, (uint)(faceRectangle.Top / heightScale), 0, 0)
                    };
                    captureImage.Children.Add(box);
                }
            }
        }

        private async void IdentifyFace_Click(object sender, RoutedEventArgs e)
        {
            using (Stream s = await photoFile.OpenStreamForReadAsync())
            {
                var faces = await faceServiceClient.DetectAsync(s);
                status.Text = "faces were detected...";
                var faceIds = faces.Select(face => face.FaceId).ToArray();
                status.Text = "the number of faceids found is: " + faceIds.Length;

                StringBuilder resultText = new StringBuilder();
                try
                {
                    var results = await faceServiceClient.IdentifyAsync(personGroupId, faceIds);

                    status.Text = " The results are reaedy";

                    if (results.Length > 0)
                        //resultText.Append($"{results.Length} face(s) detected: \t");
                        status.Text = $"{results.Length} face(s) detected: \t";

                    foreach (var identityResult in results)
                    {
                        if (identityResult.Candidates.Length != 0)
                        {
                            var candidateId = identityResult.Candidates[0].PersonId;
                            var person = await faceServiceClient.GetPersonAsync(personGroupId, candidateId);
                            resultText.Append($"Authorized User Detected: {person.Name}\t  Door will be unlocked. \t");
                            await Timer_Tick();

                            pinValue = GpioPinValue.Low;
                            pin.Write(pinValue);
                            pin.SetDriveMode(GpioPinDriveMode.Output);

                        }
                    }
                    if (resultText.ToString().Equals($"{results.Length} face(s) detected! \t"))
                    {
                        status.Text = "Cannot unlock door."; 
                        resultText.Append("No persons identified\t");
                    }
                    status.Text = resultText.ToString();

                }
                catch (FaceAPIException ex)
                {
                    status.Text = "An error occurred of type:" + ex.ErrorCode; //ex.message
                }

                //traingroup.IsEnabled = false;
                //detect_init.IsEnabled = false; //generate the person group
                //traingroup.IsEnabled = false;
                //cleanup.IsEnabled = true;

            }
        }

        private async void NewPersonGroup_Click(object sender, RoutedEventArgs e)
        {
            CreatePersonResult guest = await faceServiceClient.CreatePersonAsync(personGroupId, "Guest");

            //if (guest != null)
            //{
            //    await faceServiceClient.DeletePersonAsync(personGroupId, guest.PersonId); 
            //    //The data of the past user has been deleted
            //}

            Stream s = await photoFile.OpenStreamForReadAsync();
            await faceServiceClient.AddPersonFaceAsync(personGroupId, guest.PersonId, s);

            // Training of the new user will now begin. 
            status.Text = "Training of the new user will now begin"; 
            await faceServiceClient.TrainPersonGroupAsync(personGroupId);
            TrainingStatus sstatus = null;

            while (true)
            {
                sstatus = await faceServiceClient.GetPersonGroupTrainingStatusAsync(personGroupId);

                if (sstatus.Status != Status.Running)
                {
                    status.Text = "Person group training complete";
                    break;
                }
                await Task.Delay(1000);
            }
            status.Text = "Training of the new user has been completed. ";
        }

        private async void GeneratePersonGroup_Click(object sender, RoutedEventArgs e)
        {
            CreatePersonResult yve = await faceServiceClient.CreatePersonAsync(personGroupId, "Yvette");
           // StorageFile yveImageDir = @"C:\Data\Users\DefaultAccount\Pictures\YveFolder\636294255522489954.jpg\";
            Stream s = await photoFile.OpenStreamForReadAsync();
            await faceServiceClient.AddPersonFaceAsync(personGroupId, yve.PersonId, s);


            //The training of the users above. 
            await faceServiceClient.TrainPersonGroupAsync(personGroupId);
            TrainingStatus sstatus = null;

            while (true)
            {
                sstatus = await faceServiceClient.GetPersonGroupTrainingStatusAsync(personGroupId);

                if (sstatus.Status != Status.Running)
                {
                    status.Text = "Person group training complete";
                    break;
                }
                await Task.Delay(1000);
            }
        }

        //private async void TrainGroup_Click(object sender, RoutedEventArgs e)
        //{
        //    await faceServiceClient.TrainPersonGroupAsync(personGroupId);
        //    TrainingStatus sstatus = null;

        //    while (true)
        //    {
        //        sstatus = await faceServiceClient.GetPersonGroupTrainingStatusAsync(personGroupId);

        //        if (sstatus.Status != Status.Running)
        //        {
        //            status.Text = "Person group training complete";
        //            break;
        //        }
        //        await Task.Delay(1000);
        //        //IdentifyFace();
        //    }
        //    //face_init.IsEnabled = true;
        //    //identify_init.IsEnabled = true;
        //    //traingroup.IsEnabled = false;
        //    //detect_init.IsEnabled = false; //generate the person group
        //    //cleanup.IsEnabled = true;
        //}


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
            //face_init.IsEnabled = true;
            //identify_init.IsEnabled = false;
            //traingroup.IsEnabled = false;
            //detect_init.IsEnabled = false; //generate the person group
            //traingroup.IsEnabled = false;
            //cleanup.IsEnabled = true;
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
