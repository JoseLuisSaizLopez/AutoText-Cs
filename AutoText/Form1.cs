using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace AutoText
{
    public partial class frame : Form
    {
        public frame()
        {
            MinimumSize = new Size(600, 500);
            InitializeComponent();
            //Añadimos las imágenes a la listview
            ImageList listaImagenes = new ImageList();
            listaImagenes.Images.Add(Properties.Resources.csv_icon);
            listaImagenes.ImageSize = new Size(20, 20);
            fileListView.SmallImageList = listaImagenes;
            fileListView.SmallImageList.ColorDepth = ColorDepth.Depth32Bit;
            fileListView.View = View.Details;
            fileListView.HeaderStyle = ColumnHeaderStyle.None;
            ColumnHeader h = new ColumnHeader();
            h.Width = fileListView.ClientSize.Width - SystemInformation.VerticalScrollBarWidth;
            fileListView.Columns.Add(h);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            abrirSelectorCarpeta();
        }


        string dirPath = "";

        private void abrirSelectorCarpeta()
        {
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                pathLabel.Text = folderBrowserDialog.SelectedPath;
                dirPath = folderBrowserDialog.SelectedPath;
                cargarArchivos();
            }
        }


        private void cargarArchivos()
        {
            DirectoryInfo d = new DirectoryInfo(dirPath);
            if (d.Exists)
            {
                FileInfo[] files = d.GetFiles();
                //Vaciar lista actual
                fileListView.Items.Clear();
                //Add into listview
                foreach (FileInfo file in files)
                {
                    //Only csv files with data
                    if (file.Extension == ".csv" && file.Length > 0)
                    {
                        ListViewItem item = new ListViewItem();
                        item.Text = file.Name;
                        item.ImageIndex = 0;
                        item.Checked = true;
                        fileListView.Items.Add(item);
                    }
                }
            }
            else
            {
                Debug.WriteLine("La carpeta NO existe");
            }
        }

        bool isProcessing = false;

        private void processButton_Click(object sender, EventArgs e)
        {
            if (!isProcessing)
            {
                isProcessing = true;
                procesarArchivos();
            }
        }

        private void procesarArchivos()
        {
            if (fileListView.Items.Count > 0)
            {
                //Creamos una lista de archivos con los paths
                List<String> listaPaths = new List<string>();
                foreach (ListViewItem item in fileListView.Items)
                {
                    //Solo los que hemos seleccionado
                    if (item.Checked)
                    {
                        string path = pathLabel.Text + "\\" + item.Text;
                        Debug.WriteLine(path);
                        listaPaths.Add(path);
                    }
                }
                if (listaPaths.Count > 0)
                {
                    //Mostramos la progressbar y escondemos el boton de procesar
                    progressBar1.Value = 0;
                    setProgressBarMaximumProgress(listaPaths);
                    progressBar1.Step = 1;
                    progressBar1.Visible = true;
                    processButton.Visible = false;
                    fileListView.CheckBoxes = false;
                    button1.Enabled = false;
                    reloadButton.Enabled = false;
                    checkBox1.Enabled = false;
                    delimitador.Enabled = false;
                    checkBox2.Enabled = false;
                    label2.Enabled = false;
                    checkBox3.Enabled = false;

                    Thread processThread = new Thread(() =>
                    {
                        //Creamos la carpeta de destino
                        string folderPath = pathLabel.Text + "\\process_txt_" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                        Directory.CreateDirectory(folderPath);
                        //Procesamos cada archivo por separado
                        foreach (string csvPath in listaPaths)
                        {
                            processSingleFile(folderPath, csvPath);
                        }
                        //Comprobamos el checkbox del backup
                        if (checkBox3.Checked)
                        {
                            generarBackup(pathLabel.Text, folderPath);
                        }
                        //Abrimos la carpeta en el explorador de archivos para ver el resultado
                        ProcessStartInfo startInfo = new ProcessStartInfo
                        {
                            Arguments = folderPath,
                            FileName = "explorer.exe"
                        };
                        Process.Start(startInfo);
                        //Volvemos a hacer visible el botón de procesar y escondemos la progressbar
                        progressBar1.BeginInvoke(new MethodInvoker(() => progressBar1.Visible = false));
                        processButton.BeginInvoke(new MethodInvoker(() => processButton.Visible = true));
                        fileListView.BeginInvoke(new MethodInvoker(() => fileListView.CheckBoxes = true));
                        button1.BeginInvoke(new MethodInvoker(() => button1.Enabled = true));
                        reloadButton.BeginInvoke(new MethodInvoker(() => reloadButton.Enabled = true));
                        checkBox1.BeginInvoke(new MethodInvoker(() => checkBox1.Enabled = true));
                        checkBox1.BeginInvoke(new MethodInvoker(() => checkForEnabledDelimiters()));
                        checkBox3.BeginInvoke(new MethodInvoker(() => checkBox3.Enabled = true));
                        isProcessing = false;
                    });
                    processThread.IsBackground = true;
                    processThread.Start();
                }
                else
                {
                    //No se puede procesar, volvemos a activar el botón
                    isProcessing = false;
                }
            }
            else
            {
                //No se puede procesar, volvemos a activar el botón
                isProcessing = false;
            }
        }

        private void setProgressBarMaximumProgress(List<string> paths)
        {
            progressBar1.Maximum = paths.Count;
            //Archivos separados (if delimiador)
            int archivosSeparados = 0;
            if (checkBox1.Checked)
            {
                foreach (string filePath in paths)
                {
                    int lines = File.ReadAllLines(filePath).Length;
                    int fileCount = (int)Math.Ceiling(lines / delimitador.Value);
                    if (fileCount > 1)
                    {
                        archivosSeparados += fileCount;
                        progressBar1.Maximum += fileCount;
                    }
                }
            }
            //Backup (if backup checked)
            if (checkBox3.Checked)
            {
                int notProcessedCount = Directory.GetFiles(dirPath).Length;
                progressBar1.Maximum += notProcessedCount;
                int processedCount = paths.Count + archivosSeparados;
                progressBar1.Maximum += processedCount;
            }

        }

        private void generarBackup(string sourcePath, string outputPath)
        {
            //Create directory
            Directory.CreateDirectory(outputPath + "\\backup");
            string date = DateTime.Now.ToString("yyyyMMdd");
            Directory.CreateDirectory(outputPath + "\\backup\\" + date);
            //Get files not processed
            DirectoryInfo dIn = new DirectoryInfo(sourcePath);
            FileInfo[] filesIn = dIn.GetFiles();
            //////////////progressBar1.BeginInvoke(new MethodInvoker(() => progressBar1.Maximum += filesIn.Length));
            foreach (FileInfo file in filesIn)
            {
                File.Copy(file.FullName, outputPath + "\\backup\\" + date + "\\" + file.Name);
                progressBar1.BeginInvoke(new MethodInvoker(() => ++progressBar1.Value));
            }
            //Get processed files
            DirectoryInfo dPro = new DirectoryInfo(outputPath);
            FileInfo[] filesPro = dPro.GetFiles();
            ///////////////progressBar1.BeginInvoke(new MethodInvoker(() => progressBar1.Maximum += filesPro.Length));
            foreach (FileInfo file in filesPro)
            {
                File.Copy(file.FullName, outputPath + "\\backup\\" + date + "\\" + file.Name);
                //ProgressBar bug fix
                if (progressBar1.Maximum == progressBar1.Value)
                {
                    progressBar1.BeginInvoke(new MethodInvoker(() => ++progressBar1.Maximum));
                }
                progressBar1.BeginInvoke(new MethodInvoker(() => ++progressBar1.Value));
            }
        }


        private void processSingleFile(string folderPath, string csvPath)
        {
            //Leemos el archivo y cambiamos los ';' y ',' por tabulaciones
            StringBuilder outputText = new StringBuilder();
            int numeroMaxLineas = (int)delimitador.Value;
            int lineaActual = 0;
            int numeroArchivo = 1;
            foreach (string linea in File.ReadLines(csvPath))
            {
                string lineaFormat = linea.Replace(";", "\t").Replace(",", "\t");
                //Substituimos los acentos
                lineaFormat = lineaFormat
                    //A
                    .Replace("á", "a").Replace("Á", "A")
                    .Replace("à", "a").Replace("À", "A")
                    .Replace("ã", "a").Replace("Ã", "A")
                    .Replace("ä", "a").Replace("Ä", "A")
                    //E
                    .Replace("é", "e").Replace("É", "E")
                    .Replace("è", "e").Replace("È", "E")
                    .Replace("ë", "e").Replace("Ë", "E")
                    //I
                    .Replace("í", "i").Replace("Í", "I")
                    .Replace("ì", "i").Replace("Ì", "I")
                    .Replace("ï", "i").Replace("Ï", "I")
                    //O
                    .Replace("ó", "o").Replace("Ó", "O")
                    .Replace("ò", "o").Replace("Ò", "O")
                    .Replace("õ", "o").Replace("Õ", "O")
                    .Replace("ö", "o").Replace("Ö", "O")
                    //U
                    .Replace("ú", "u").Replace("Ú", "U")
                    .Replace("ù", "u").Replace("Ù", "U")
                    .Replace("ü", "u").Replace("Ü", "U");
                outputText.AppendLine(lineaFormat);
                lineaActual++;
                if (lineaActual == numeroMaxLineas && checkBox1.Checked)
                {
                    ///////////progressBar1.BeginInvoke(new MethodInvoker(() => ++progressBar1.Maximum));
                    procesarArchivoSeparado(outputText, folderPath, csvPath, numeroArchivo);
                    ++numeroArchivo;
                    StringBuilder nuevaCadena = new StringBuilder();
                    lineaActual = 0;
                    if (checkBox2.Checked)
                    {
                        nuevaCadena.AppendLine(outputText.ToString().Substring(0, outputText.ToString().IndexOf(Environment.NewLine)));
                        lineaActual ++;
                    }
                    outputText = nuevaCadena;
                }
            }
            if (numeroArchivo == 1)
            {
                //Creamos el txt dentro de la carpeta destino con el contenido actualizado
                string[] segmentedPath = csvPath.Split('\\');
                string fileName = segmentedPath[segmentedPath.Length - 1];
                fileName = fileName.Substring(0, fileName.Length - 4) + ".txt";
                try
                {
                    using (FileStream fs = File.Create(folderPath + "\\" + fileName))
                    {
                        byte[] contenido = new UTF8Encoding(true).GetBytes(outputText.ToString());
                        fs.Write(contenido, 0, contenido.Length);
                    }
                }
                catch
                {
                    Debug.WriteLine("No se ha podido escribir el archivo...");
                }
                progressBar1.BeginInvoke(new MethodInvoker(() => progressBar1.Value++));
            }
            else
            {
                procesarArchivoSeparado(outputText, folderPath, csvPath, numeroArchivo);
            }
        }


        private void procesarArchivoSeparado(StringBuilder outputText, string folderPath, string csvPath, int numeroArchivo)
        {
            //Creamos el txt dentro de la carpeta destino con el contenido actualizado
            string[] segmentedPath = csvPath.Split('\\');
            string fileName = segmentedPath[segmentedPath.Length - 1];
            fileName = fileName.Substring(0, fileName.Length - 4) + "--"+numeroArchivo.ToString()+".txt";
            try
            {
                using (FileStream fs = File.Create(folderPath + "\\" + fileName))
                {
                    byte[] contenido = new UTF8Encoding(true).GetBytes(outputText.ToString());
                    fs.Write(contenido, 0, contenido.Length);
                }
            }
            catch
            {
                Debug.WriteLine("No se ha podido escribir el archivo...");
            }
            //ProgressBar bug fix
            if (progressBar1.Maximum == progressBar1.Value)
            {
                progressBar1.BeginInvoke(new MethodInvoker(() => ++progressBar1.Maximum));
            }
            progressBar1.BeginInvoke(new MethodInvoker(() => ++progressBar1.Value));
        }


        protected override void OnClosed(EventArgs e)
        {
            Environment.Exit(Environment.ExitCode);
            base.OnClosed(e);
        }

        private void reloadButton_Click(object sender, EventArgs e)
        {
            if (dirPath != "")
            {
                rotateImage(reloadButton.Image);
                cargarArchivos();
            }
        }


        bool rotating = false;

        private void rotateImage(Image rotateImage)
        {
            if (!rotating)
            {
                reloadButton.Invalidate();

                rotating = true;
                Thread rotateAnimThread = new Thread(() =>
                {
                    rotateImage.RotateFlip(RotateFlipType.Rotate90FlipNone);
                    reloadButton.BeginInvoke(new MethodInvoker(() => reloadButton.Invalidate()));
                    Thread.Sleep(50);
                    rotateImage.RotateFlip(RotateFlipType.Rotate90FlipNone);
                    reloadButton.BeginInvoke(new MethodInvoker(() => reloadButton.Invalidate()));
                    Thread.Sleep(50);
                    rotateImage.RotateFlip(RotateFlipType.Rotate90FlipNone);
                    reloadButton.BeginInvoke(new MethodInvoker(() => reloadButton.Invalidate()));
                    Thread.Sleep(50);
                    rotateImage.RotateFlip(RotateFlipType.Rotate90FlipNone);
                    reloadButton.BeginInvoke(new MethodInvoker(() => reloadButton.Invalidate()));
                    rotating = false;
                });
                rotateAnimThread.IsBackground = true;
                rotateAnimThread.Start();
            }

        }

        private void checkForEnabledDelimiters()
        {
            delimitador.Enabled = checkBox1.Checked;
            checkBox2.Enabled = checkBox1.Checked;
            label2.Enabled = checkBox1.Checked;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            checkForEnabledDelimiters();
        }
    }
}
