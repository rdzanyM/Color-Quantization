using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Color_Quantization
{
    public partial class Form1 : Form
    {
        Image original = null;
        byte[] levels = { 2, 2, 2 };
        public Form1()
        {
            InitializeComponent();
            pictureBox.SizeMode = PictureBoxSizeMode.CenterImage;
            FlowersToolStripMenuItem_Click(null, null);
            TextBox1_Leave(null, null);
            TextBox2_Leave(null, null);
            TextBox3_Leave(null, null);
        }

        private void LoadImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Select Image";
                dlg.Filter = "bmp files (*.bmp)|*.bmp|jpg files (*.jpg)|*.jpg";
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    original = new Bitmap(dlg.FileName);
                    comboBox.SelectedIndex = 0;
                    ComboBox_SelectedIndexChanged(null, null);
                }
            }
        }

        Bitmap Scale(Image i)
        {
            double d = Math.Max((double)i.Width / pictureBox.Width, (double)i.Height / pictureBox.Height);
            if (d <= 1) return new Bitmap(i);
            return new Bitmap(i, (int)(i.Width / d), (int)(i.Height / d));
        }

        private void ComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            Redraw();
        }

        private void Redraw()
        {
            Bitmap bitmap = Scale(original);
            DirectBitmap direct = new DirectBitmap(bitmap, bitmap.Width, bitmap.Height);
            switch (comboBox.SelectedIndex)
            {
                case 1:
                    AverageDithering(direct);
                    break;
                case 2:
                    RandomDithering(direct);
                    break;
            }
            pictureBox.Image = direct.Bitmap;
            pictureBox.Refresh();
        }

        /// <summary>
        /// About equal number of pixels with specific color component value.
        /// It may not be the case if the picture has many pixels with same color component value.
        /// </summary>
        /// <param name="direct"></param>
        private void AverageDithering(DirectBitmap direct)
        {
            int d;
            List<byte> list = new List<byte>();
            List<byte> threshholds = new List<byte>();
            byte[,,] rgb = new byte[3, direct.Width, direct.Height];
            Parallel.For(0, direct.Width, (i) =>
            {
                for (int j = 0; j < direct.Height; j++)
                {
                    Color color = direct.GetPixel(i, j);
                    rgb[0, i, j] = color.R;
                    rgb[1, i, j] = color.G;
                    rgb[2, i, j] = color.B;
                }
            });
            for (int c = 0; c < 3; c++)
            {
                for (int i = 0; i < direct.Width; i++) for (int j = 0; j < direct.Height; j++)
                    list.Add(rgb[c, i, j]);
                list.Sort();
                for (int i = 1; i < levels[c]; i++)
                {
                    threshholds.Add(list[(i * list.Count / levels[c])]);
                }
                threshholds.Add(255);
                d = (levels[c] - 1);
                Parallel.For(0, direct.Width, (i) =>
                {
                    for (int j = 0; j < direct.Height; j++)
                        for (int k = 0; k < threshholds.Count; k++)
                        {
                            if (rgb[c, i, j] <= threshholds[k])
                            {
                                rgb[c, i, j] = (byte)(k * 255 / d);
                                break;
                            }
                        }
                });
                list.Clear();
                threshholds.Clear();
            }
            for (int i = 0; i < direct.Width; i++) for (int j = 0; j < direct.Height; j++)
                direct.SetPixel(i, j, Color.FromArgb(rgb[0, i, j], rgb[1, i, j], rgb[2, i, j]));
        }

        private void RandomDithering(DirectBitmap direct)
        {
            int d, r;
            double rd;
            byte[,,] rgb = new byte[3, direct.Width, direct.Height];
            Parallel.For(0, direct.Width, (i) =>
            {
                for (int j = 0; j < direct.Height; j++)
                {
                    Color color = direct.GetPixel(i, j);
                    rgb[0, i, j] = color.R;
                    rgb[1, i, j] = color.G;
                    rgb[2, i, j] = color.B;
                }
            });
            Random seeder = new Random();
            for (int c = 0; c < 3; c++)
            {
                d = (levels[c] - 1);
                r = 256 / d;
                rd = 256.0 / d;
                if (256 % d != 0) r++;
                int[] seeds = new int[direct.Width];
                for (int i = 0; i < seeds.Length; i++)  seeds[i] = seeder.Next();
                Parallel.For(0, direct.Width, (i) =>
                {
                    Random random = new Random(seeds[i]);   //Can't place random outside the loop as it's not thread safe.
                                                            //Default seed value is time-based, so I have to seed it manualy.
                    for (int j = 0; j < direct.Height; j++)
                    {
                        int lvl = (int)(rgb[c, i, j] / rd);
                        if (rgb[c, i, j] % r > random.Next() % r) lvl++;
                        rgb[c, i, j] = (byte)(lvl * 255 / d);
                    }
                });
            }
            for (int i = 0; i < direct.Width; i++) for (int j = 0; j < direct.Height; j++)
                direct.SetPixel(i, j, Color.FromArgb(rgb[0, i, j], rgb[1, i, j], rgb[2, i, j]));
        }

            private void TextBox1_TextChanged(object sender, EventArgs e)
        {
            if (Byte.TryParse(textBox1.Text, out byte b) && b >= 2)
            {
                levels[0] = b;
                Redraw();
            }
        }

        private void TextBox2_TextChanged(object sender, EventArgs e)
        {
            if (Byte.TryParse(textBox2.Text, out byte b) && b >= 2)
            {
                levels[1] = b;
                Redraw();
            }
        }

        private void TextBox3_TextChanged(object sender, EventArgs e)
        {
            if (Byte.TryParse(textBox3.Text, out byte b) && b >= 2)
            {
                levels[2] = b;
                Redraw();
            }
        }

        private void TextBox1_Leave(object sender, EventArgs e)
        {
            if (!Byte.TryParse(textBox1.Text, out byte b) || b < 2)
                textBox1.Text = levels[0].ToString();
        }

        private void TextBox2_Leave(object sender, EventArgs e)
        {
            if (!Byte.TryParse(textBox2.Text, out byte b) || b < 2)
                textBox2.Text = levels[1].ToString();
        }

        private void TextBox3_Leave(object sender, EventArgs e)
        {
            if (!Byte.TryParse(textBox3.Text, out byte b) || b < 2)
                textBox3.Text = levels[2].ToString();
        }

        private void FlowersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            original = Properties.Resources.Flowers;
            comboBox.SelectedIndex = 0;
            ComboBox_SelectedIndexChanged(null, null);
        }
    }
}
