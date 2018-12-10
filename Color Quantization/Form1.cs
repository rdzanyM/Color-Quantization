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
        bool noRefresh = false;
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
            if (comboBox.SelectedIndex == 5 && labelR.Visible)
            {
                labelR.Visible = false;
                labelB.Visible = false;
                labelG.Text = "Number of colors (2-999)";
                textBox1.Visible = false;
                textBox3.Visible = false;
                noRefresh = true;
                textBox2.Text = "8";
                noRefresh = false;
            }
            else if (comboBox.SelectedIndex < 5 && !labelR.Visible)
            {
                labelR.Visible = true;
                labelB.Visible = true;
                labelG.Text = "G levels";
                textBox1.Visible = true;
                textBox3.Visible = true;
                noRefresh = true;
                textBox1.Text = "2";
                textBox2.Text = "2";
                textBox3.Text = "2";
                noRefresh = false;
            }
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
                case 5:
                    Popularity(direct);
                    break;
            }
            pictureBox.Image = direct.Bitmap;
            pictureBox.Refresh();
        }


        /// <summary>
        /// About equal number of pixels with specific color component value.
        /// It may not be the case if the picture has many pixels with same color component value.
        /// This method equalizes the histogram.
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

        /// <summary>
        /// The difference between the actual color and the chosen one is propagated on the nearby pixels.
        /// </summary>
        /// <param name="filter">Determines the diretion and strength of the error propagation</param>
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
        }

        /// <summary>
        /// Choses k most popular colors in the image.
        /// Assignes one of the chosen colors to each pixel based on euclidian distance in rgb.
        /// Tries to chose colors then are not very near to each other.
        /// </summary>
        private void Popularity(DirectBitmap direct)
        {
            List<Color> chosenColors = new List<Color>();
            Dictionary<Color, int> colorCount = new Dictionary<Color, int>();
            int[,,] rgb = new int[3, direct.Width, direct.Height];
            Parallel.For(0, direct.Width, (i) =>
            {
                for (int j = 0; j < direct.Height; j++)
                {
                    Color color = direct.GetPixel(i, j);
                    rgb[0, i, j] = color.R;
                    rgb[1, i, j] = color.G;
                    rgb[2, i, j] = color.B;
                    //initial color reduction to 52 per channel so that chosen colors are less similar
                    for(int k = 0; k < 3; k++)
                    {
                        if (rgb[k, i, j] % 5 > 2)
                            rgb[k, i, j] += 2;
                        rgb[k, i, j] = rgb[k, i, j] / 5 * 5;
                    }
                }
            });
            for (int i = 0; i < direct.Width; i++)
            {
                for (int j = 0; j < direct.Height; j++)
                {
                    Color color = Color.FromArgb(rgb[0, i, j], rgb[1, i, j], rgb[2, i, j]);
                    if (colorCount.ContainsKey(color))
                        colorCount[color]++;
                    else
                        colorCount.Add(color, 1);
                }
            }
            List<Color> ordered = colorCount.OrderByDescending(pair => pair.Value).Select(pair => pair.Key).ToList();
            Dictionary<Color, Color> approximate = new Dictionary<Color, Color>();
            List<Color> reserve = new List<Color>();
            HashSet<Color> taken = new HashSet<Color>();
            HashSet<Color> discarded = new HashSet<Color>();
            foreach (Color c in ordered)
            {
                if (IsLocalMaximum40(c)) //when choosing colors we try to chose only ones that are most common in a 40x40x40 cube around them.
                {
                    chosenColors.Add(c);
                    taken.Add(c);
                }
                else
                {
                    reserve.Add(c);
                    discarded.Add(c);
                }
                if (chosenColors.Count == levels[1]) break;
            }
            if(chosenColors.Count < levels[1])
            {
                discarded.Clear();
                List<Color> reserve2 = new List<Color>();
                foreach (Color c in reserve)
                {
                    if (IsLocalMaximum10(c))
                    {
                        chosenColors.Add(c);
                        taken.Add(c);
                    }
                    else
                    {
                        reserve2.Add(c);
                        discarded.Add(c);
                    }
                    if (chosenColors.Count == levels[1]) break;
                }
                foreach (Color c in reserve2)
                {
                    if (chosenColors.Count == levels[1]) break;
                    chosenColors.Add(c);
                }
            }
            foreach(Color c in ordered)
            {
                int min = Difference(c, chosenColors[0]);
                int i = 0;
                int dif;
                for(int j = 1; j < chosenColors.Count; j++)
                {
                    dif = Difference(c, chosenColors[j]);
                    if (dif < min)
                    {
                        min = dif;
                        i = j;
                    }
                }
                approximate.Add(c, chosenColors[i]);
            }
            ;
            Parallel.For(0, direct.Width, (i) =>
            {
                for (int j = 0; j < direct.Height; j++)
                {
                    direct.SetPixel(i, j, approximate[Color.FromArgb(rgb[0, i, j], rgb[1, i, j], rgb[2, i, j])]);
                }
            });

            int Difference(Color c1, Color c2)
            {
                int dR = c1.R - c2.R;
                int dG = c1.G - c2.G;
                int dB = c1.B - c2.B;
                return dR * dR + dG * dG + dB * dB;
            }

            bool IsLocalMaximum40(Color color) //max in a cube with side 40
            {
                for (int r = Math.Max(color.R - 20, 0); r <= Math.Min(color.R + 20, 255); r+=5)
                    for (int g = Math.Max(color.G - 20, 0); g <= Math.Min(color.G + 20, 255); g+=5)
                        for (int b = Math.Max(color.B - 20, 0); b <= Math.Min(color.B + 20, 255); b+=5)
                        {
                            Color c = Color.FromArgb(r, g, b);
                            if (taken.Contains(c) || discarded.Contains(c))
                                return false;
                        }
                return true;
            }

            bool IsLocalMaximum10(Color color) //max in a cube with side 10
            {
                for (int r = Math.Max(color.R - 5, 0); r <= Math.Min(color.R + 5, 255); r += 5)
                    for (int g = Math.Max(color.G - 5, 0); g <= Math.Min(color.G + 5, 255); g += 5)
                        for (int b = Math.Max(color.B - 5, 0); b <= Math.Min(color.B + 5, 255); b += 5)
                        {
                            Color c = Color.FromArgb(r, g, b);
                            if (taken.Contains(c) || discarded.Contains(c))
                                return false;
                        }
                return true;
            }
        }



        private void TextBox1_TextChanged(object sender, EventArgs e)
        {
            if (Int32.TryParse(textBox1.Text, out int i) && 1 < i && i < 257)
            {
                levels[0] = i;
                if(!noRefresh) Redraw();
            }
        }

        private void TextBox2_TextChanged(object sender, EventArgs e)
        {
            if (Int32.TryParse(textBox2.Text, out int i) && 1 < i && (i < 257 || (!labelR.Visible && i < 1e4)))
            {
                levels[1] = i;
                if (!noRefresh) Redraw();
            }
        }

        private void TextBox3_TextChanged(object sender, EventArgs e)
        {
            if (Int32.TryParse(textBox3.Text, out int i) && 1 < i && i < 257)
            {
                levels[2] = i;
                if (!noRefresh) Redraw();
            }
        }

        private void TextBox1_Leave(object sender, EventArgs e)
        {
            if (!Int32.TryParse(textBox1.Text, out int i) || i < 2 || 256 < i)
                textBox1.Text = levels[0].ToString();
        }

        private void TextBox2_Leave(object sender, EventArgs e)
        {
            if (!Int32.TryParse(textBox2.Text, out int i) || i < 2 || (256 < i && labelR.Visible) || 9999 < i)
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
