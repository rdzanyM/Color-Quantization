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
        /// <summary>
        /// Number of colors per channel(R,G,B)
        /// </summary>
        int[] levels = { 2, 2, 2 };
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
                case 3:
                    OrderedDithering(direct);
                    break;
                case 4:
                    ErrorPropagation(direct, Filters.FloydAndSteinberg_Filter);
                    break;
            }
            pictureBox.Image = direct.Bitmap;
            pictureBox.Refresh();
        }

        /// <summary>
        /// About equal number of pixels with specific color component value.
        /// It may not be the case if the picture has many pixels with same color component value.
        /// This method increases the contrast of an image
        /// </summary>
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
            Parallel.For(0, direct.Width, (i) =>
            {
                for (int j = 0; j < direct.Height; j++)
                    direct.SetPixel(i, j, Color.FromArgb(rgb[0, i, j], rgb[1, i, j], rgb[2, i, j]));
            });
        }

        /// <summary>
        /// <para>
        /// Each pixel is assigned one of the 2 levels closest to its color.
        /// The lower the difference between the level and the color, the higher the probability it will be assigned.
        /// The algorithm is non-deterministic
        /// </para>
        /// <example>
        /// E.g. when converting to 3 colors (0,127,255)
        /// the color 150 will be converted to 127(82%) or 255(18%).
        /// </example>
        /// </summary>
        private void RandomDithering(DirectBitmap direct)
        {
            int lvlMax;
            double lvlWidth;
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
                lvlMax = (levels[c] - 1);
                lvlWidth = 255.0 / lvlMax;
                int[] seeds = new int[direct.Width];
                for (int i = 0; i < seeds.Length; i++)  seeds[i] = seeder.Next();
                Parallel.For(0, direct.Width, (i) =>
                {
                    Random random = new Random(seeds[i]);   //Can't place random outside the loop as it's not thread safe.
                                                            //Default seed value is time-based, so I have to seed it manualy.
                    for (int j = 0; j < direct.Height; j++)
                    {
                        double lvlD = rgb[c, i, j] / lvlWidth;
                        int lvl = (int)lvlD;
                        if (lvlD - lvl > random.NextDouble()) lvl++;
                        rgb[c, i, j] = (byte)(lvl * 255 / lvlMax);
                    }
                });
            }
            Parallel.For(0, direct.Width, (i) =>
            {
                for (int j = 0; j < direct.Height; j++)
                    direct.SetPixel(i, j, Color.FromArgb(rgb[0, i, j], rgb[1, i, j], rgb[2, i, j]));
            });
        }

        /// <summary>
        /// <para>
        /// Each pixel is assigned one of the 2 levels closest to its color.
        /// The lower the difference between the level and the color, the higher the probability it will be assigned.
        /// </para>
        /// <para>
        /// Unlike <see cref="RandomDithering(DirectBitmap)"/> the algorithm is deterministic.
        /// The color is determined by pixel position and the order matrix.
        /// </para>
        /// <example>
        /// E.g. when converting to 3 colors (0,127,255)
        /// the color 150 will be converted to 127(82%) or 255(18%).
        /// </example>
        /// </summary>
        private void OrderedDithering(DirectBitmap direct)
        {
            int lvlMax, n2, n;
            double[,] orderMatrix; // n x n
            double lvlWidth;
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
                lvlMax = (levels[c] - 1);
                n2 = 255 / lvlMax;
                lvlWidth = 255.0 / lvlMax;
                if (255 % lvlMax > 0) n2++;
                orderMatrix = Normalize(GetMatrix(n2));
                n = orderMatrix.GetLength(0);
                n2 = n * n;
                Parallel.For(0, direct.Width, (i) =>
                {
                    for (int j = 0; j < direct.Height; j++)
                    {
                        double lvlD = rgb[c, i, j] / lvlWidth;
                        int lvl = (int)lvlD;
                        if ((lvlD - lvl) > orderMatrix[i % n, j % n]) lvl++;
                        rgb[c, i, j] = (byte)(lvl * 255 / lvlMax);
                    }
                });
            }
            Parallel.For(0, direct.Width, (i) =>
            {
                for (int j = 0; j < direct.Height; j++)
                    direct.SetPixel(i, j, Color.FromArgb(rgb[0, i, j], rgb[1, i, j], rgb[2, i, j]));
            });

            int[,] GetMatrix(int el)    //gets order matrix with at least 'el' elements
            {
                if (el == 1)
                    return new int[1, 1] { { 0 } };
                if (4 < el && el < 10)
                    return new int[3, 3] { { 6, 8, 4 }, { 1, 0, 3 }, { 5, 2, 7 } };
                el += 3;
                el /= 4;
                return Expand(GetMatrix(el));

                int[,] Expand(int[,] m) //expands order matrix m so that it has 4 times more elements
                {
                    int k = m.GetLength(0);
                    int[,] e = new int[2 * k, 2 * k];
                    Parallel.For(0, 2 * k, (i) =>
                    {
                        for (int j = 0; j < 2 * k; j++)
                        {
                            e[i, j] = m[i % k, j % k] * 4;
                            if (i >= k)
                            {
                                if (j < k)
                                    e[i, j] += 2;
                                else
                                    e[i, j]++;
                            }
                            else if (j >= k)
                                e[i, j] += 3;
                        }
                    });
                    return e;
                }
            }
            double[,] Normalize(int[,] m) //divides all elements of matrix m(n x n) by n^2
            {
                int l = m.GetLength(0);
                double ll = l * l;
                double[,] normal = new double[l,l];
                Parallel.For(0, l, (i) =>
                {
                    for (int j = 0; j < l; j++)
                        normal[i, j] = m[i, j] / ll;
                });
                return normal;
            }
        }

        private void ErrorPropagation(DirectBitmap direct, double[,] filter)
        {
            double[,,] rgb = new double[3, direct.Width, direct.Height];
            byte[,,] result = new byte [3, direct.Width, direct.Height];
            int X = (filter.GetLength(0) - 1) / 2;
            int Y = (filter.GetLength(1) - 1) / 2;
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

            Parallel.For(0, 3, (c) =>   //only 3 threads, the order of pixel processing is important.
            {
                double d = 255.0 / (levels[c] - 1);
                for (int j = 0; j < direct.Height; j++)
                {
                    for (int i = 0; i < direct.Width; i++)
                    {
                        result[c, i, j] = (byte)((byte)Math.Round(rgb[c, i, j] / d) * d);
                        double error = rgb[c, i, j] - result[c, i, j];
                        for(int x = 0; x <= X; x++) //upper half of filter array is filled with 0.
                        {
                            for(int y = -Y; y <= Y; y++)
                            {
                                int a = i + y;
                                int b = j + x;
                                if (a >= direct.Width)
                                    a = direct.Width - 1;
                                else if (a < 0)
                                    a = 0;
                                if (b >= direct.Height)
                                    b = direct.Height - 1;
                                rgb[c, a, b] += error * filter[X + x, Y + y];
                            }
                        }
                    }
                }
            });

            Parallel.For(0, direct.Width, (i) =>
            {
                for (int j = 0; j < direct.Height; j++)
                    direct.SetPixel(i, j, Color.FromArgb(result[0, i, j], result[1, i, j], result[2, i, j]));
            });
            return;
        }

        private void TextBox1_TextChanged(object sender, EventArgs e)
        {
            if (Int32.TryParse(textBox1.Text, out int i) && 1 < i && i < 257)
            {
                levels[0] = i;
                Redraw();
            }
        }

        private void TextBox2_TextChanged(object sender, EventArgs e)
        {
            if (Int32.TryParse(textBox2.Text, out int i) && 1 < i && i < 257)
            {
                levels[1] = i;
                Redraw();
            }
        }

        private void TextBox3_TextChanged(object sender, EventArgs e)
        {
            if (Int32.TryParse(textBox3.Text, out int i) && 1 < i && i < 257)
            {
                levels[2] = i;
                Redraw();
            }
        }

        private void TextBox1_Leave(object sender, EventArgs e)
        {
            if (!Int32.TryParse(textBox1.Text, out int i) || i < 2 || 256 < i)
                textBox1.Text = levels[0].ToString();
        }

        private void TextBox2_Leave(object sender, EventArgs e)
        {
            if (!Int32.TryParse(textBox2.Text, out int i) || i < 2 || 256 < i)
                textBox2.Text = levels[1].ToString();
        }

        private void TextBox3_Leave(object sender, EventArgs e)
        {
            if (!Int32.TryParse(textBox3.Text, out int i) || i < 2 || 256 < i)
                textBox3.Text = levels[2].ToString();
        }

        private void FlowersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            original = Properties.Resources.Flowers;
            comboBox.SelectedIndex = 0;
            ComboBox_SelectedIndexChanged(null, null);
        }

        private void ParrotsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            original = Properties.Resources.Parrots;
            comboBox.SelectedIndex = 0;
            ComboBox_SelectedIndexChanged(null, null);
        }

        private void IceCreamToolStripMenuItem_Click(object sender, EventArgs e)
        {
            original = Properties.Resources.IceCream;
            comboBox.SelectedIndex = 0;
            ComboBox_SelectedIndexChanged(null, null);
        }

        private void JellyBeansToolStripMenuItem_Click(object sender, EventArgs e)
        {
            original = Properties.Resources.JellyBeans;
            comboBox.SelectedIndex = 0;
            ComboBox_SelectedIndexChanged(null, null);
        }
    }
}
