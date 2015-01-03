using System;
using System.Windows.Forms;
using System.Threading;

namespace GARCTool
{
    public partial class Interface : Form
    {
        public Interface()
        {
            InitializeComponent();
            this.DragEnter += Form_DragEnter;
            this.DragDrop += Form_DragDrop;
        }
        private void Form_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }
        private void Form_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 1)
            {
                DialogResult dr = Util.Prompt(MessageBoxButtons.YesNoCancel, "Process multiple GARC files?");
                if (dr == DialogResult.Cancel)
                    return;
                else if (dr == DialogResult.No)
                    TB_Path.Text = files[0]; // open first D&D
                else
                {
                    Array.Sort(files);
                    Thread thread = new Thread(() => batchThread(files));
                    thread.Start();
                }
            }
            else
                TB_Path.Text = files[0]; // open first D&D
        }

        private void B_Open_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == DialogResult.OK)
                TB_Path.Text = fbd.SelectedPath;
        }
        private void B_OpenFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == DialogResult.OK)
                TB_Path.Text = ofd.FileName;
        }
        private void B_Process_Click(object sender, EventArgs e)
        {
            if (TB_Path.Text.Length == 0)
            { Util.Alert("No Path Loaded"); return; }
            Thread thread = new Thread(() => mainThread(false));
            thread.Start();
        }

        private void mainThread(bool supress = false)
        {
            GARCTool.garcOmni(TB_Path.Text, ModifierKeys == Keys.Control, progressBar, ProgressLabel, supress);
        }
        private void batchThread(string[] files)
        {
            for (int i = 0; i < files.Length; i++)
            {
                if (System.IO.Directory.Exists(files[i])) continue; // Not doing Folder-> GARC operations.
                if (TB_Path.InvokeRequired)
                    TB_Path.Invoke((MethodInvoker)delegate { TB_Path.Text = files[i]; });
                else TB_Path.Text = files[i];
                int threadctr = System.Diagnostics.Process.GetCurrentProcess().Threads.Count;
                Thread thread = new Thread(() => mainThread(true));
                thread.Start();
                while (thread.IsAlive)
                    Thread.Sleep(200); // Pause cpu to let it execute fully.
            }
            Util.Alert(files.Length + " files processed.");
        }
    }
}