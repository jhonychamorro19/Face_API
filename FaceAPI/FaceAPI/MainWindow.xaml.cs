using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.ProjectOxford.Common.Contract;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;

namespace FaceTutorial
{
    public partial class MainWindow : Window
    {
        /*NOTA: las claves de suscripción de prueba gratuita se generan en la región de westcentralus, por lo que si está utilizando
        una clave de suscripción de prueba gratuita, no debería necesitar cambiar esta región.
        Remplace el _key_ con su API Face*/
        private readonly IFaceServiceClient faceServiceClient =
            new FaceServiceClient("_key_", "https://westcentralus.api.cognitive.microsoft.com/face/v1.0");

        Face[] faces;                   // La lista de caras detectadas.
        String[] faceDescriptions;      // La lista de descripciones para las caras detectadas.
        double resizeFactor;            // El factor de cambio de tamaño para la imagen mostrada.

        public MainWindow()
        {
            InitializeComponent();
        }        

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            //Busca una imagen proporcionada por el usuario
            var openDlg = new Microsoft.Win32.OpenFileDialog();

            openDlg.Filter = "JPEG Image(*.jpg)|*.jpg";
            bool? result = openDlg.ShowDialog(this);

            // Regresa si se cancela
            if (!(bool)result)
            {
                return;
            }

            // Muestra el archivo de imagen
            string filePath = openDlg.FileName;

            Uri fileUri = new Uri(filePath);
            BitmapImage bitmapSource = new BitmapImage();

            bitmapSource.BeginInit();
            bitmapSource.CacheOption = BitmapCacheOption.None;
            bitmapSource.UriSource = fileUri;
            bitmapSource.EndInit();

            FacePhoto.Source = bitmapSource;

            // Detecta rostros en la imagen
            Title = "Detectando ...";
            faces = await UploadAndDetectFaces(filePath);
            Title = String.Format("Detección Finalizada. {0} rostro(s) detectados", faces.Length);

            if (faces.Length > 0)
            {
                // Prepara para dibujar rectangulos en las caras
                DrawingVisual visual = new DrawingVisual();
                DrawingContext drawingContext = visual.RenderOpen();
                drawingContext.DrawImage(bitmapSource,
                    new Rect(0, 0, bitmapSource.Width, bitmapSource.Height));
                double dpi = bitmapSource.DpiX;
                resizeFactor = 96 / dpi;
                faceDescriptions = new String[faces.Length];

                for (int i = 0; i < faces.Length; ++i)
                {
                    Face face = faces[i];

                    // Dibuja rectangulos en los rostros
                    drawingContext.DrawRectangle(
                        Brushes.Transparent,
                        new Pen(Brushes.Red, 4),
                        new Rect(
                            face.FaceRectangle.Left * resizeFactor,
                            face.FaceRectangle.Top * resizeFactor,
                            face.FaceRectangle.Width * resizeFactor,
                            face.FaceRectangle.Height * resizeFactor
                            )
                    );

                    // Almacena la descripcion del rostro
                    faceDescriptions[i] = FaceDescription(face);
                }

                drawingContext.Close();

                // Muestra la imagen con los cuadros al rededor del rostro
                RenderTargetBitmap faceWithRectBitmap = new RenderTargetBitmap(
                    (int)(bitmapSource.PixelWidth * resizeFactor),
                    (int)(bitmapSource.PixelHeight * resizeFactor),
                    96,
                    96,
                    PixelFormats.Pbgra32);

                faceWithRectBitmap.Render(visual);
                FacePhoto.Source = faceWithRectBitmap;

                // Estado en la barra de texto
                faceDescriptionStatusBar.Text = "Coloque el puntero del mouse sobre una cara para ver la descripción de la cara.";
            }
        }

        // muestra las descripciones de los rostros mientras el mouse pasa por el cuadro.

        private void FacePhoto_MouseMove(object sender, MouseEventArgs e)
        {
            
            if (faces == null)
                return;
            
            Point mouseXY = e.GetPosition(FacePhoto);

            ImageSource imageSource = FacePhoto.Source;
            BitmapSource bitmapSource = (BitmapSource)imageSource;
            
            var scale = FacePhoto.ActualWidth / (bitmapSource.PixelWidth / resizeFactor);
            
            bool mouseOverFace = false;

            for (int i = 0; i < faces.Length; ++i)
            {
                FaceRectangle fr = faces[i].FaceRectangle;
                double left = fr.Left * scale;
                double top = fr.Top * scale;
                double width = fr.Width * scale;
                double height = fr.Height * scale;
                
                if (mouseXY.X >= left && mouseXY.X <= left + width && mouseXY.Y >= top && mouseXY.Y <= top + height)
                {
                    faceDescriptionStatusBar.Text = faceDescriptions[i];
                    mouseOverFace = true;
                    break;
                }
            }

            // Si el mouse no está sobre el cuadro
            if (!mouseOverFace)
                faceDescriptionStatusBar.Text = "Coloque el puntero del mouse sobre una cara para ver la descripción de la cara.";
        }        

        private async Task<Face[]> UploadAndDetectFaces(string imageFilePath)
        {            
            IEnumerable<FaceAttributeType> faceAttributes =
                new FaceAttributeType[] { FaceAttributeType.Gender, FaceAttributeType.Age, FaceAttributeType.Smile, FaceAttributeType.Emotion, FaceAttributeType.Glasses, FaceAttributeType.Hair };

            // Llama al Face API.
            try
            {
                using (Stream imageFileStream = File.OpenRead(imageFilePath))
                {
                    Face[] faces = await faceServiceClient.DetectAsync(imageFileStream, returnFaceId: true, returnFaceLandmarks: false, returnFaceAttributes: faceAttributes);
                    return faces;
                }
            }
            // Captura y muestra errores de Face API.
            catch (FaceAPIException f)
            {
                MessageBox.Show(f.ErrorMessage, f.ErrorCode);
                return new Face[0];
            }
            // Captura y muestra todos los otros errores.
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error");
                return new Face[0];
            }
        }        

        private string FaceDescription(Face face)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("Face: ");

            // Agrega el sexo, la edad y la sonrisa.
            sb.Append(face.FaceAttributes.Gender);
            sb.Append(", ");
            sb.Append(face.FaceAttributes.Age);
            sb.Append(", ");
            sb.Append(String.Format("smile {0:F1}%, ", face.FaceAttributes.Smile * 100));

            // Agrega las emociones. Muestra todas las emociones más del 10%.
            sb.Append("Emotion: ");
            EmotionScores emotionScores = face.FaceAttributes.Emotion;
            if (emotionScores.Anger >= 0.1f) sb.Append(String.Format("anger {0:F1}%, ", emotionScores.Anger * 100));
            if (emotionScores.Contempt >= 0.1f) sb.Append(String.Format("contempt {0:F1}%, ", emotionScores.Contempt * 100));
            if (emotionScores.Disgust >= 0.1f) sb.Append(String.Format("disgust {0:F1}%, ", emotionScores.Disgust * 100));
            if (emotionScores.Fear >= 0.1f) sb.Append(String.Format("fear {0:F1}%, ", emotionScores.Fear * 100));
            if (emotionScores.Happiness >= 0.1f) sb.Append(String.Format("happiness {0:F1}%, ", emotionScores.Happiness * 100));
            if (emotionScores.Neutral >= 0.1f) sb.Append(String.Format("neutral {0:F1}%, ", emotionScores.Neutral * 100));
            if (emotionScores.Sadness >= 0.1f) sb.Append(String.Format("sadness {0:F1}%, ", emotionScores.Sadness * 100));
            if (emotionScores.Surprise >= 0.1f) sb.Append(String.Format("surprise {0:F1}%, ", emotionScores.Surprise * 100));

            // Agrega lentes.
            sb.Append(face.FaceAttributes.Glasses);
            sb.Append(", ");

            // Agrega cabello.
            sb.Append("Hair: ");

            // Muestra la confianza de la calvicie si supera el 1%.
            if (face.FaceAttributes.Hair.Bald >= 0.01f)
                sb.Append(String.Format("bald {0:F1}% ", face.FaceAttributes.Hair.Bald * 100));

            //Muestra todos los atributos de color de pelo por encima del 10 %.
            HairColor[] hairColors = face.FaceAttributes.Hair.HairColor;
            foreach (HairColor hairColor in hairColors)
            {
                if (hairColor.Confidence >= 0.1f)
                {
                    sb.Append(hairColor.Color.ToString());
                    sb.Append(String.Format(" {0:F1}% ", hairColor.Confidence * 100));
                }
            }            
            return sb.ToString();
        }
    }
}