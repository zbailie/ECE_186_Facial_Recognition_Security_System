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
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Storage; 
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media.Imaging;
using Microsoft.ProjectOxford.Face;
//using VideoFrameAnalyzer;
using Windows.Media.Core;
using Windows.Media.Devices;
using Microsoft.ProjectOxford.Video;
using Windows.Graphics.Imaging;
using Windows.Devices.Enumeration;
using System.Threading;
using Windows.UI.Core;
using System.Threading.Tasks;
using Windows.Storage.Pickers;



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

        private SoftwareBitmap _backBuffer;
        private bool _taskRunning = false;
        private Image _imageElement; 
        private readonly FaceServiceClient faceServiceClient = new FaceServiceClient("7703c42821cf4256942c61faf78062ce");

        private FaceDetectionEffect _faceDetectionEffect;

        private MediaCapture mediaCapture;
        private StorageFile photoFile;
        private StorageFile recordStorageFile;
        private StorageFile audioFile;
        private readonly string PHOTO_FILE_NAME = "photo.jpg";
        private readonly string VIDEO_FILE_NAME = "video.mp4";
        private readonly string AUDIO_FILE_NAME = "audio.mp3";
        private bool isPreviewing;
        private bool isRecording;

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
                audio_init.IsEnabled = true;
            }
            else
            {
                video_init.IsEnabled = false;
                audio_init.IsEnabled = false;
            }
        }

        /// <summary>
        /// Helper function to enable or disable video related buttons (TakePhoto, Start Video Record)
        /// </summary>
        /// <param name="action">enum Action</param>
        private void SetVideoButtonVisibility(Action action)
        {
            if (action == Action.ENABLE)
            {
                takePhoto.IsEnabled = true;
                takePhoto.Visibility = Visibility.Visible;

                recordVideo.IsEnabled = true;
                recordVideo.Visibility = Visibility.Visible;
            }
            else
            {
                takePhoto.IsEnabled = false;
                takePhoto.Visibility = Visibility.Collapsed;

                recordVideo.IsEnabled = false;
                recordVideo.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Helper function to enable or disable audio related buttons (Start Audio Record)
        /// </summary>
        /// <param name="action">enum Action</param>
        private void SetAudioButtonVisibility(Action action)
        {
            if (action == Action.ENABLE)
            {
                recordAudio.IsEnabled = true;
                recordAudio.Visibility = Visibility.Visible;
            }
            else
            {
                recordAudio.IsEnabled = false;
                recordAudio.Visibility = Visibility.Collapsed;
            }
        }
        #endregion
        public MainPage()
        {
            this.InitializeComponent();

            SetInitButtonVisibility(Action.ENABLE);
            SetVideoButtonVisibility(Action.DISABLE);
            SetAudioButtonVisibility(Action.DISABLE);

            isRecording = false;
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
                    captureImage.Source = null;
                    playbackElement.Source = null;
                    isPreviewing = false;
                }
                if (isRecording)
                {
                    await mediaCapture.StopRecordAsync();
                    isRecording = false;
                    recordVideo.Content = "Start Video Record";
                    recordAudio.Content = "Start Audio Record";
                }
                mediaCapture.Dispose();
                mediaCapture = null;



            }
            SetInitButtonVisibility(Action.ENABLE);
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
            SetVideoButtonVisibility(Action.DISABLE);
            SetAudioButtonVisibility(Action.DISABLE);

            try
            {
                if (mediaCapture != null)
                {
                    // Cleanup MediaCapture object
                    if (isPreviewing)
                    {
                        await mediaCapture.StopPreviewAsync();
                        captureImage.Source = null;
                        playbackElement.Source = null;
                        isPreviewing = false;
                    }
                    if (isRecording)
                    {
                        await mediaCapture.StopRecordAsync();
                        isRecording = false;
                        recordVideo.Content = "Start Video Record";
                        recordAudio.Content = "Start Audio Record";
                    }
                    mediaCapture.Dispose();
                    mediaCapture = null;
                }

                status.Text = "Initializing camera to capture audio and video...";
                // Use default initialization
                mediaCapture = new MediaCapture();
                await mediaCapture.InitializeAsync();

                // Set callbacks for failure and recording limit exceeded
                status.Text = "Device successfully initialized for video recording!";
                mediaCapture.Failed += new MediaCaptureFailedEventHandler(mediaCapture_Failed);
                mediaCapture.RecordLimitationExceeded += new Windows.Media.Capture.RecordLimitationExceededEventHandler(mediaCapture_RecordLimitExceeded);

                // Start Preview                
                previewElement.Source = mediaCapture;
                await mediaCapture.StartPreviewAsync();
                isPreviewing = true;
                status.Text = "Camera preview succeeded";


                // ---------------------------------------------------------------------------------------------------------
                // -------------- The stuff for the frame source -----------------------------------------------------------
                //Take out the frames from the camera preview
                captureImage.Source = null;
                // Get information about the preview
                var previewProperties = mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview) as VideoEncodingProperties;

                // Create a video frame in the desired format for the preview frame
                VideoFrame videoFrame = new VideoFrame(BitmapPixelFormat.Bgra8, (int)previewProperties.Width, (int)previewProperties.Height);

                VideoFrame previewFrame = await mediaCapture.GetPreviewFrameAsync(videoFrame);

                SoftwareBitmap previewBitmap = previewFrame.SoftwareBitmap;



                

                var outputFile= await KnownFolders.PicturesLibrary.CreateFileAsync(
                    PHOTO_FILE_NAME, CreationCollisionOption.GenerateUniqueName);
                ImageEncodingProperties imageProperties = ImageEncodingProperties.CreateJpeg();
                await mediaCapture.CapturePhotoToStorageFileAsync(imageProperties, photoFile);
               // takePhoto.IsEnabled = true;
                status.Text = "Take Photo succeeded: " + photoFile.Path;



                //FileSavePicker fileSavePicker = new FileSavePicker();
                //fileSavePicker.SuggestedStartLocation = KnownFolders.PicturesLibrary;
                //fileSavePicker.FileTypeChoices.Add("JPEG files", new List<string>() { ".jpg" });
                //fileSavePicker.SuggestedFileName = "image";

                //var outputFile = await fileSavePicker.PickSaveFileAsync();

                if (outputFile == null)
                {
                    // The user cancelled the picking operation
                    return;
                }

                SaveSoftwareBitmapToFile(previewBitmap, outputFile);

                var filepath = outputFile.Path;

                //using (Stream s = (Stream)photoStream)
                //{
                var faces = await faceServiceClient.DetectAsync(filepath, true, true);

                foreach (var face in faces)
                {
                    var rect = face.FaceRectangle;
                    var landmarks = face.FaceLandmarks;
                }



                previewFrame.Dispose();
                previewFrame = null;



                IRandomAccessStream photoStream = await outputFile.OpenReadAsync();
                //BitmapImage photoFile = new BitmapImage();
                BitmapImage bitmap = new BitmapImage();
                //photoFile.SetSource(photoStream);
                bitmap.SetSource(photoStream);
                //captureImage.Source = photoFile;
                captureImage.Source = bitmap;

                

                //After this, call the frame that was just identified to be displayed in one of the three panes of
                // the XAML 
                //https://social.msdn.microsoft.com/Forums/expression/en-US/33bc4fa3-9997-4fd0-8445-ee4de41d8076/uwpdisplay-image-byte-based-in-xaml-image-source?forum=wpdevelop
                //


                string sourceDire = @"C:\Data\Users\DefaultAccount\Pictures";
                deletephoto(sourceDire);


                //// Deleting all the File from a memory location
                //string sourceDir = @"C:\Data\Users\DefaultAccount\Pictures";

                //try
                //{
                //    string[] photos = Directory.GetFiles(sourceDir, "*.jpg");

                //    //copy picture files from the above location
                //    foreach(string f in photos)
                //    {
                //        // Remove path from the file name
                //        string fName = f.Substring(sourceDir.Length + 1);
                //    }
                    
                //    foreach( string f in photos)
                //    {
                //        File.Delete(f);
                //    }
                    
                //}
                //catch(DirectoryNotFoundException dirNotFound)
                //{
                //    Console.WriteLine(dirNotFound);
                //}

                //----------------------------------------------------------------------

                // Enable buttons for video and photo capture
                SetVideoButtonVisibility(Action.ENABLE);

                // Enable Audio Only Init button, leave the video init button disabled
                audio_init.IsEnabled = true;
            }
            catch (Exception ex)
            {
                status.Text = "Unable to initialize camera for audio/video mode: " + ex.Message;
            }
        }

        private static void deletephoto(string sourceDir)
        {


            // Deleting all the File from a memory location

            try
            {
                string[] photos = Directory.GetFiles(sourceDir, "*.jpg");

                //copy picture files from the above location
                foreach (string f in photos)
                {
                    // Remove path from the file name
                    string fName = f.Substring(sourceDir.Length + 1);
                }

                foreach (string f in photos)
                {
                    File.Delete(f);
                }

            }
            catch (DirectoryNotFoundException dirNotFound)
            {
                Console.WriteLine(dirNotFound);
            }
        }

        private void cleanup_Click(object sender, RoutedEventArgs e)
        {
            SetInitButtonVisibility(Action.DISABLE);
            SetVideoButtonVisibility(Action.DISABLE);
            SetAudioButtonVisibility(Action.DISABLE);
            Cleanup();
        }



        //Function that stores the Softwarebitmap image to a file
        private async void SaveSoftwareBitmapToFile(SoftwareBitmap softwareBitmap, StorageFile outputFile)
        {
            using (IRandomAccessStream stream = await outputFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                // Create an encoder with the desired format
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);

                // Set the software bitmap
                encoder.SetSoftwareBitmap(softwareBitmap);

                // Set additional encoding parameters, if needed
                encoder.BitmapTransform.ScaledWidth = 320;
                encoder.BitmapTransform.ScaledHeight = 240;
                encoder.BitmapTransform.Rotation = Windows.Graphics.Imaging.BitmapRotation.Clockwise90Degrees;
                encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
                encoder.IsThumbnailGenerated = true;

                try
                {
                    await encoder.FlushAsync();
                }
                catch (Exception err)
                {
                    switch (err.HResult)
                    {
                        case unchecked((int)0x88982F81): //WINCODEC_ERR_UNSUPPORTEDOPERATION
                                                         // If the encoder does not support writing a thumbnail, then try again
                                                         // but disable thumbnail generation.
                            encoder.IsThumbnailGenerated = false;
                            break;
                        default:
                            throw err;
                    }
                }

                if (encoder.IsThumbnailGenerated == false)
                {
                    await encoder.FlushAsync();
                }


            }
        }

        //Function to read the frames from the video preview
        /*   private async void rect(string[] args)
             {
                 FaceDetectionEffect _faceDetectionEffect; 

                 // Create the definition, which will contain some initialization settings
                 var definition = new FaceDetectionEffectDefinition();

                 // To ensure preview smoothness, do not delay incoming samples
                 definition.SynchronousDetectionEnabled = false;

                 // In this scenario, choose detection speed over accuracy
                 definition.DetectionMode = FaceDetectionMode.HighPerformance;

                 // Add the effect to the preview stream
                 _faceDetectionEffect = (FaceDetectionEffect)await mediaCapture.AddVideoEffectAsync(definition, MediaStreamType.VideoPreview);

                 // Choose the shortest interval between detection events
                 _faceDetectionEffect.DesiredDetectionInterval = TimeSpan.FromMilliseconds(33);

                 // Start detecting faces
                 _faceDetectionEffect.Enabled = true;

             }

             private void FaceDetectionEffect_FaceDetected(FaceDetectionEffect sender, FaceDetectedEventArgs args)
             {
                 //public struct BitmapBounds;

                 foreach (Windows.Media.FaceAnalysis.DetectedFace face in args.ResultFrame.DetectedFaces)
                 {
                     BitmapBounds faceRect = face.FaceBox;

                     // Draw a rectangle on the preview stream for each face
                 }
             }

         */

        /// <summary>
        /// 'Initialize Audio Only' button action function
        /// Dispose existing MediaCapture object and set it up for audio only
        /// Enable or disable appropriate buttons
        /// - DISABLE 'Initialize Audio Only' 
        /// - DISABLE 'Start Video Record'
        /// - DISABLE 'Take Photo'
        /// - ENABLE 'Initialize Audio and Video'
        /// - ENABLE 'Start Audio Record'        
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void initAudioOnly_Click(object sender, RoutedEventArgs e)
        {
            // Disable all buttons until initialization completes
            SetInitButtonVisibility(Action.DISABLE);
            SetVideoButtonVisibility(Action.DISABLE);
            SetAudioButtonVisibility(Action.DISABLE);

            try
            {
                if (mediaCapture != null)
                {
                    // Cleanup MediaCapture object
                    if (isPreviewing)
                    {
                        await mediaCapture.StopPreviewAsync();
                        captureImage.Source = null;
                        playbackElement.Source = null;
                        isPreviewing = false;
                    }
                    if (isRecording)
                    {
                        await mediaCapture.StopRecordAsync();
                        isRecording = false;
                        recordVideo.Content = "Start Video Record";
                        recordAudio.Content = "Start Audio Record";
                    }
                    mediaCapture.Dispose();
                    mediaCapture = null;
                }

                status.Text = "Initializing camera to capture audio only...";
                mediaCapture = new MediaCapture();
                var settings = new Windows.Media.Capture.MediaCaptureInitializationSettings();
                settings.StreamingCaptureMode = Windows.Media.Capture.StreamingCaptureMode.Audio;
                settings.MediaCategory = Windows.Media.Capture.MediaCategory.Other;
                settings.AudioProcessing = Windows.Media.AudioProcessing.Default;
                await mediaCapture.InitializeAsync(settings);

                // Set callbacks for failure and recording limit exceeded
                status.Text = "Device successfully initialized for audio recording!" + "\nPress \'Start Audio Record\' to record";
                mediaCapture.Failed += new MediaCaptureFailedEventHandler(mediaCapture_Failed);
                mediaCapture.RecordLimitationExceeded += new Windows.Media.Capture.RecordLimitationExceededEventHandler(mediaCapture_RecordLimitExceeded);

                // Enable buttons for audio
                SetAudioButtonVisibility(Action.ENABLE);

                // Enable Audio and video Only Init button
                video_init.IsEnabled = true;
            }
            catch (Exception ex)
            {
                status.Text = "Unable to initialize camera for audio mode: " + ex.Message;
            }
        }

        /// <summary>
        /// 'Take Photo' button click action function
        /// Capture image to a file in the default account photos folder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void takePhoto_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                takePhoto.IsEnabled = false;
                recordVideo.IsEnabled = false;
                captureImage.Source = null;

                photoFile = await KnownFolders.PicturesLibrary.CreateFileAsync(
                    PHOTO_FILE_NAME, CreationCollisionOption.GenerateUniqueName);
                ImageEncodingProperties imageProperties = ImageEncodingProperties.CreateJpeg();
                await mediaCapture.CapturePhotoToStorageFileAsync(imageProperties, photoFile);
                takePhoto.IsEnabled = true;
                status.Text = "Take Photo succeeded: " + photoFile.Path;

                System.Console.WriteLine("The photofile path: {0}!", photoFile.Path); 

                //IRandomAccessStream photoStream = await photoFile.OpenReadAsync(); 
                ////BitmapImage photoFile = new BitmapImage();
                //BitmapImage bitmap = new BitmapImage(); 
                ////photoFile.SetSource(photoStream);
                //bitmap.SetSource(photoStream);
                ////captureImage.Source = photoFile;
                //captureImage.Source = bitmap;



                //var picture = photoFile.Path;

                //Call the rect face identification here
                // using the path (bitmap)or(photoFile)

            //    //using (Stream s = (Stream)photoStream)
            //    //{
            //    var faces = await faceServiceClient.DetectAsync(picture, true, true);

            //        foreach (var face in faces)
            //        {
            //            var rect = face.FaceRectangle;
            //            var landmarks = face.FaceLandmarks;

            //        }
            //    }

            }

            catch (Exception ex)
            {
                status.Text = ex.Message;
                Cleanup();
            }
            finally
            {
                takePhoto.IsEnabled = true;
                recordVideo.IsEnabled = true;
            }
        }


        /// <summary>
        /// 'Start Video Record' button click action function
        /// Button name is changed to 'Stop Video Record' once recording is started
        /// Records video to a file in the default account videos folder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void recordVideo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                takePhoto.IsEnabled = false;
                recordVideo.IsEnabled = false;
                playbackElement.Source = null;

                if (recordVideo.Content.ToString() == "Start Video Record")
                {
                    takePhoto.IsEnabled = false;
                    status.Text = "Initialize video recording";
                    String fileName;
                    fileName = VIDEO_FILE_NAME;

                    recordStorageFile = await Windows.Storage.KnownFolders.VideosLibrary.CreateFileAsync(fileName, Windows.Storage.CreationCollisionOption.GenerateUniqueName);

                    status.Text = "Video storage file preparation successful";

                    MediaEncodingProfile recordProfile = null;
                    recordProfile = MediaEncodingProfile.CreateMp4(Windows.Media.MediaProperties.VideoEncodingQuality.Auto);

                    await mediaCapture.StartRecordToStorageFileAsync(recordProfile, recordStorageFile);
                    recordVideo.IsEnabled = true;
                    recordVideo.Content = "Stop Video Record";
                    isRecording = true;
                    status.Text = "Video recording in progress... press \'Stop Video Record\' to stop";
                }
                else
                {
                    takePhoto.IsEnabled = true;
                    status.Text = "Stopping video recording...";
                    await mediaCapture.StopRecordAsync();
                    isRecording = false;





                    var stream = await recordStorageFile.OpenReadAsync();
                    playbackElement.AutoPlay = true;
                    playbackElement.SetSource(stream, recordStorageFile.FileType);
                    playbackElement.Play();
                    status.Text = "Playing recorded video" + recordStorageFile.Path;
                    recordVideo.Content = "Start Video Record";
                }
            }
            catch (Exception ex)
            {
                if (ex is System.UnauthorizedAccessException)
                {
                    status.Text = "Unable to play recorded video; video recorded successfully to: " + recordStorageFile.Path;
                    recordVideo.Content = "Start Video Record";
                }
                else
                {
                    status.Text = ex.Message;
                    Cleanup();
                }
            }
            finally
            {
                recordVideo.IsEnabled = true;
            }
        }

        /// <summary>
        /// 'Start Audio Record' button click action function
        /// Button name is changes to 'Stop Audio Record' once recording is started
        /// Records audio to a file in the default account video folder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void recordAudio_Click(object sender, RoutedEventArgs e)
        {
            recordAudio.IsEnabled = false;
            playbackElement3.Source = null;

            try
            {
                if (recordAudio.Content.ToString() == "Start Audio Record")
                {
                    audioFile = await Windows.Storage.KnownFolders.VideosLibrary.CreateFileAsync(AUDIO_FILE_NAME, Windows.Storage.CreationCollisionOption.GenerateUniqueName);

                    status.Text = "Audio storage file preparation successful";

                    MediaEncodingProfile recordProfile = null;
                    recordProfile = MediaEncodingProfile.CreateM4a(Windows.Media.MediaProperties.AudioEncodingQuality.Auto);

                    await mediaCapture.StartRecordToStorageFileAsync(recordProfile, audioFile);

                    isRecording = true;
                    recordAudio.IsEnabled = true;
                    recordAudio.Content = "Stop Audio Record";
                    status.Text = "Audio recording in progress... press \'Stop Audio Record\' to stop";
                }
                else
                {
                    status.Text = "Stopping audio recording...";

                    await mediaCapture.StopRecordAsync();

                    isRecording = false;
                    recordAudio.IsEnabled = true;
                    recordAudio.Content = "Start Audio Record";

                    var stream = await audioFile.OpenAsync(Windows.Storage.FileAccessMode.Read);
                    status.Text = "Playback recorded audio: " + audioFile.Path;
                    playbackElement3.AutoPlay = true;
                    playbackElement3.SetSource(stream, audioFile.FileType);
                    playbackElement3.Play();
                }
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
                Cleanup();
            }
            finally
            {
                recordAudio.IsEnabled = true;
            }
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
                    status.Text = "MediaCaptureFailed: " + currentFailure.Message;

                    if (isRecording)
                    {
                        await mediaCapture.StopRecordAsync();
                        status.Text += "\n Recording Stopped";
                    }
                }
                catch (Exception)
                {
                }
                finally
                {
                    SetInitButtonVisibility(Action.DISABLE);
                    SetVideoButtonVisibility(Action.DISABLE);
                    SetAudioButtonVisibility(Action.DISABLE);
                    status.Text += "\nCheck if camera is diconnected. Try re-launching the app";
                }
            });
        }

        /// <summary>
        /// Callback function if Recording Limit Exceeded
        /// </summary>
        /// <param name="currentCaptureObject"></param>
        public async void mediaCapture_RecordLimitExceeded(Windows.Media.Capture.MediaCapture currentCaptureObject)
        {
            try
            {
                if (isRecording)
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                    {
                        try
                        {
                            status.Text = "Stopping Record on exceeding max record duration";
                            await mediaCapture.StopRecordAsync();
                            isRecording = false;
                            recordAudio.Content = "Start Audio Record";
                            recordVideo.Content = "Start Video Record";
                            if (mediaCapture.MediaCaptureSettings.StreamingCaptureMode == StreamingCaptureMode.Audio)
                            {
                                status.Text = "Stopped record on exceeding max record duration: " + audioFile.Path;
                            }
                            else
                            {
                                status.Text = "Stopped record on exceeding max record duration: " + recordStorageFile.Path;
                            }
                        }
                        catch (Exception e)
                        {
                            status.Text = e.Message;
                        }
                    });
                }
            }
            catch (Exception e)
            {
                status.Text = e.Message;
            }
        }
    }
}
