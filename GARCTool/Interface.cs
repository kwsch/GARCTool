using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace GARCTool
{
    public partial class Interface : Form
    {
        public Interface()
        {
            InitializeComponent();
            this.DragEnter += new DragEventHandler(Form_DragEnter);
            this.DragDrop += new DragEventHandler(Form_DragDrop);
        }
        private void Form_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }
        private void Form_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            string path = files[0]; // open first D&D
            TB_Path.Text = path;
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
            { MessageBox.Show("No Path Loaded"); return; }
            GARCTool.garcOmni(TB_Path.Text, progressBar);
        }
    }
}
