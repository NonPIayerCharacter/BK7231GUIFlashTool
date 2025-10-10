﻿using System;
using System.Text;
using System.Windows.Forms;

namespace BK7231Flasher
{
    public partial class FormMain : Form, ILogListener
    {
        private void tabPage2_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void tabPage2_DragDrop(object sender, DragEventArgs e)
        {
            textBoxTuyaCFGJSON.Text = "";
            textBoxTuyaCFGText.Text = "";
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string file in files)
            {
				importTuyaConfig(file);
			}
		}
        private void buttonImportConfigFileDialog_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();

            // Set the file dialog properties
            //openFileDialog1.InitialDirectory = "c:\\";
            openFileDialog1.Filter = "All files (*.*)|*.*";
            openFileDialog1.FilterIndex = 1;
            openFileDialog1.RestoreDirectory = true;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string selectedFile = openFileDialog1.FileName;

                    // Call the importTuyaConfig method with the selected file
                    importTuyaConfig(selectedFile);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: Could not read file from disk. Original error: " + ex.Message);
                }
            }
        }

        public void importTuyaConfig(string file) 
        {
            try
            {
                // Do something with the dropped file(s)
                TuyaConfig tc = new TuyaConfig();
                if (tc.fromFile(file) == false)
                {
                    if (tc.extractKeys() == false)
                    {
                        textBoxTuyaCFGJSON.Text = tc.getKeysAsJSON();
                        textBoxTuyaCFGText.Text = tc.getKeysHumanReadable();
                    }
                    else
                    {
                        MessageBox.Show("Failed to extract keys");
                    }
                }
                else
                {
                    if(tc.isLastBinaryOBKConfig())
                    {
                        MessageBox.Show("The file you've dragged looks like OBK config, not a Tuya one.");
                    }
                    else if (tc.isLastBinaryFullOf0xff())
                    {
                        MessageBox.Show("Failed, it seems that given binary is an erased flash sector, full of 0xFF");
                    }
                    else
                    {
                        MessageBox.Show("Failed, see log for more details");
                    }
                }
            }
            catch (Exception ex)
            {
                textBoxTuyaCFGText.Text = "Sorry, exception occured: " + ex.ToString();
            }
        }

        private void chkChangeKey_CheckedChanged(object sender, EventArgs e)
        {
            txtKey.Visible = chkChangeKey.Checked;
            lblKeyInfo.Visible = chkChangeKey.Checked;
            if(!chkChangeKey.Checked) txtKey.Text = "8710_2M";
        }

        private void txtKey_TextChanged(object sender, EventArgs e)
        {
            TuyaConfig.KEY_PART_1 = Encoding.ASCII.GetBytes(txtKey.Text);
        }
    }
}
