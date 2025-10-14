using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Collections.Generic;

namespace PixelArtEditor
{
    public partial class Form1 : Form
    {
        private static readonly Color FUNDOPADRAOBTN = Color.FromArgb(60, 60, 64);
        private static readonly Color MOUSEHOVERBTNCOLOR = Color.FromArgb(15, 62, 138);

        private const int GridWidth = 32;
        private const int GridHeight = 32;

        private Bitmap canvasBitmap;
        private Color drawColor = Color.Black;
        private Color secondaryColor = Color.Red;

        private Panel colorPrimaryPanel;
        private Panel colorSecondaryPanel;
        private Panel[] quickColorPanels = new Panel[15];

        private Stack<Bitmap> undoStack = new Stack<Bitmap>();
        private const int MaxUndo = 20;

        private bool isDrawing = false;
        private float zoom = 16f;
        private float zoomIncrement = 1.1f;

        private Panel panelLeft;
        private Panel panelRight;
        private Panel panelBottom;
        private Panel panelCanvas;
        private Button btnNovo;
        private Button btnExportar;

        private int OffsetX => (int)((panelCanvas.ClientSize.Width - GridWidth * zoom) / 2);
        private int OffsetY => (int)((panelCanvas.ClientSize.Height - GridHeight * zoom) / 2);
        private Point MousePositionCanvas;



        // ==================== Ferramentas ====================
        private enum Ferramenta { Lapiz, Borracha, Balde, Retangulo, Circulo }
        private Ferramenta ferramentaAtual = Ferramenta.Lapiz;
        private Point? pontoFinal = null;
        private Point? pontoInicial = null;
        private bool mouseDentroCanvas = false;
        private Button btnLapis;
        private Button btnBorracha;
        private Button btnBalde;
        private Bitmap previewBitmap = null;


        public Form1()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            this.WindowState = FormWindowState.Maximized;
            this.Text = "Pixel Art Editor";
            this.KeyPreview = true;

            canvasBitmap = new Bitmap(GridWidth, GridHeight);
            CriarLayout();
        }

        private void CriarLayout()
        {
            // ==================== Painel esquerdo ====================
            panelLeft = new Panel { Dock = DockStyle.Left, Width = 100, BackColor = Color.FromArgb(45, 45, 48) };
            this.Controls.Add(panelLeft);

            CriarFerramentas();

            // Define botão ativo inicial
            SetActiveButton(btnLapis);
            ferramentaAtual = Ferramenta.Lapiz;


            // ==================== Painel direito ====================
            panelRight = new Panel { Dock = DockStyle.Right, Width = 200, BackColor = Color.FromArgb(37, 37, 38) };
            this.Controls.Add(panelRight);

            // Cor primária
            colorPrimaryPanel = CriarPainelCor(drawColor, 20, 20);
            colorPrimaryPanel.Click += (s, e) => EscolherCor(ref drawColor, colorPrimaryPanel);
            panelRight.Controls.Add(colorPrimaryPanel);

            // Cor secundária
            colorSecondaryPanel = CriarPainelCor(secondaryColor, 80, 20);
            colorSecondaryPanel.Click += (s, e) => EscolherCor(ref secondaryColor, colorSecondaryPanel);
            panelRight.Controls.Add(colorSecondaryPanel);

            // Paleta rápida
            int startY = 90, padding = 5, panelSize = 30;
            for (int i = 0; i < 15; i++)
            {
                Panel p = new Panel
                {
                    BackColor = Color.Transparent,
                    Width = panelSize,
                    Height = panelSize,
                    Location = new Point(20 + (i % 5) * (panelSize + padding), startY + (i / 5) * (panelSize + padding)),
                    BorderStyle = BorderStyle.FixedSingle,
                    Cursor = Cursors.Hand
                };
                panelRight.Controls.Add(p);
                quickColorPanels[i] = p;

                p.MouseDown += (s, e) =>
                {
                    Panel panelClicado = s as Panel;
                    if (panelClicado.BackColor == Color.Transparent) return;

                    if (Control.ModifierKeys == Keys.Shift) // remove cor
                        panelClicado.BackColor = Color.Transparent;
                    else if (e.Button == MouseButtons.Left)
                    {
                        drawColor = panelClicado.BackColor;
                        colorPrimaryPanel.BackColor = drawColor;
                    }
                    else if (e.Button == MouseButtons.Right)
                    {
                        secondaryColor = panelClicado.BackColor;
                        colorSecondaryPanel.BackColor = secondaryColor;
                    }
                };
            }

            // Cores padrão
            Color[] coresPadrao = { Color.Black, Color.Red, Color.Brown, Color.Blue, Color.Orange };
            for (int i = 0; i < coresPadrao.Length; i++)
                quickColorPanels[i].BackColor = coresPadrao[i];

            // ==================== Painel inferior ====================
            panelBottom = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Color.FromArgb(30, 30, 30) };
            this.Controls.Add(panelBottom);

            btnNovo = new Button { Text = "Novo", Width = 100, Height = 30, Location = new Point(10, 10) };
            btnExportar = new Button { Text = "Exportar", Width = 100, Height = 30, Location = new Point(120, 10) };
            panelBottom.Controls.AddRange(new Control[] { btnNovo, btnExportar });

            // ==================== Canvas ====================
            panelCanvas = new Panel { Dock = DockStyle.Fill, BackColor = Color.DimGray };
            this.Controls.Add(panelCanvas);
            AtivarDoubleBuffer(panelCanvas);

            panelCanvas.Paint += PanelCanvas_Paint;
            panelCanvas.MouseDown += PanelCanvas_MouseDown;
            panelCanvas.MouseMove += PanelCanvas_MouseMove;
            panelCanvas.MouseUp += PanelCanvas_MouseUp;
            panelCanvas.MouseWheel += PanelCanvas_MouseWheel;
            panelCanvas.MouseEnter += (s, e) => { mouseDentroCanvas = true; pontoFinal = null; };
            panelCanvas.MouseLeave += (s, e) => { mouseDentroCanvas = false; isDrawing = false; pontoFinal = null; };

            this.KeyDown += Form1_KeyDown;
        }

        // ==================== Funções auxiliares ====================
        private Panel CriarPainelCor(Color cor, int x, int y)
        {
            return new Panel
            {
                BackColor = cor,
                Width = 50,
                Height = 50,
                Location = new Point(x, y),
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand
            };
        }

        private void EscolherCor(ref Color targetColor, Panel painel)
        {
            using (ColorDialog dlg = new ColorDialog())
            {
                dlg.Color = targetColor;
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    targetColor = dlg.Color;
                    painel.BackColor = targetColor;
                    AdicionarCorRapida(targetColor);
                }
            }
        }

        private void AdicionarCorRapida(Color cor)
        {
            foreach (var p in quickColorPanels)
            {
                if (p.BackColor == Color.Transparent)
                {
                    p.BackColor = cor;
                    break;
                }
            }
        }

        // ===============================
        // 🧰 CRIAÇÃO DE FERRAMENTAS
        // ===============================

        private void CriarFerramentas()
        {
            // 🖌️ Criação dos botões de ferramentas com o enum correto
            btnLapis = CriarBotaoFerramenta("🖉 Lápis", 10, Ferramenta.Lapiz);
            btnBorracha = CriarBotaoFerramenta("🧽 Borracha", 50, Ferramenta.Borracha);
            btnBalde = CriarBotaoFerramenta("🪣 Balde", 90, Ferramenta.Balde);
            Button btnRetangulo = CriarBotaoFerramenta("▭ Retângulo", 130, Ferramenta.Retangulo);
            Button btnCirculo = CriarBotaoFerramenta("● Círculo", 170, Ferramenta.Circulo);

            // Adiciona ao painel
            panelLeft.Controls.Add(btnLapis);
            panelLeft.Controls.Add(btnBorracha);
            panelLeft.Controls.Add(btnBalde);
            panelLeft.Controls.Add(btnRetangulo);
            panelLeft.Controls.Add(btnCirculo);

            // Define o botão ativo inicialmente
            SetActiveButton(btnLapis);
            ferramentaAtual = Ferramenta.Lapiz;
        }

        // ===============================
        // 🧱 MÉTODO DE CRIAÇÃO DE BOTÕES
        // ===============================

        private Button CriarBotaoFerramenta(string texto, int posY, Ferramenta ferramenta)
        {
            Button btn = new Button
            {
                Text = texto,
                Width = 90,
                Height = 30,
                Location = new Point(5, posY),
                BackColor = FUNDOPADRAOBTN,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            // Quando clicar, muda a ferramenta atual
            btn.Click += (s, e) =>
            {
                ferramentaAtual = ferramenta;
                SetActiveButton(btn);
            };

            AddHoverEffect(btn);
            return btn;
        }



        private void SetActiveButton(Button activeBtn)
        {
            foreach (var ctrl in panelLeft.Controls)
                if (ctrl is Button btn) btn.BackColor = FUNDOPADRAOBTN;

            activeBtn.BackColor = Color.DarkCyan;
        }

        private void AddHoverEffect(Button btn)
        {
            btn.MouseEnter += (s, e) => { if (btn.BackColor != Color.DarkCyan) btn.BackColor = MOUSEHOVERBTNCOLOR; };
            btn.MouseLeave += (s, e) => { if (btn.BackColor != Color.DarkCyan) btn.BackColor = FUNDOPADRAOBTN; };
        }

        private void AtivarDoubleBuffer(Control c)
        {
            typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(c, true, null);
        }

        private void SalvarParaUndo()
        {
            if (undoStack.Count >= MaxUndo)
                undoStack.Pop();
            undoStack.Push((Bitmap)canvasBitmap.Clone());
        }

        // ==================== Eventos de desenho ====================
        private void PanelCanvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
            {
                SalvarParaUndo();

                int x = (int)((e.X - OffsetX) / zoom);
                int y = (int)((e.Y - OffsetY) / zoom);
                if (x < 0 || x >= GridWidth || y < 0 || y >= GridHeight) return;

                if (ferramentaAtual == Ferramenta.Retangulo || ferramentaAtual == Ferramenta.Circulo)
                {
                    pontoInicial = new Point(x, y);
                    pontoFinal = null; // para usar no preview
                }
                else
                {
                    isDrawing = true;
                    DrawPixel(e.Location, e.Button);
                }

                panelCanvas.Invalidate();
            }
        }

        private void PanelCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            MousePositionCanvas = e.Location;

            if ((ferramentaAtual == Ferramenta.Retangulo || ferramentaAtual == Ferramenta.Circulo) && pontoInicial.HasValue)
            {
                panelCanvas.Invalidate(); // redesenha o preview do retângulo ou círculo
            }
            else if (isDrawing)
            {
                DrawPixel(e.Location, e.Button);
            }
        }

        private void PanelCanvas_MouseUp(object sender, MouseEventArgs e)
        {
            int mouseX = (int)((e.X - OffsetX) / zoom);
            int mouseY = (int)((e.Y - OffsetY) / zoom);
            mouseX = Math.Max(0, Math.Min(GridWidth - 1, mouseX));
            mouseY = Math.Max(0, Math.Min(GridHeight - 1, mouseY));

            if (ferramentaAtual == Ferramenta.Retangulo && pontoInicial.HasValue)
            {
                pontoFinal = new Point(mouseX, mouseY);
                Color corRetangulo = (e.Button == MouseButtons.Left) ? drawColor : secondaryColor;
                DesenharRetangulo(pontoInicial.Value, pontoFinal.Value, corRetangulo);

                pontoInicial = null;
                pontoFinal = null;
                panelCanvas.Invalidate();
            }
            else if (ferramentaAtual == Ferramenta.Circulo && pontoInicial.HasValue)
            {
                Point centro = pontoInicial.Value;
                Point borda = new Point(mouseX, mouseY);
                Color corCirculo = (e.Button == MouseButtons.Left) ? drawColor : secondaryColor;
                DesenharCirculo(centro, borda, corCirculo);

                pontoInicial = null;
                pontoFinal = null;
                panelCanvas.Invalidate();
            }

            isDrawing = false;

            // Limpa pontoFinal do lápis/borracha
            if (ferramentaAtual == Ferramenta.Lapiz || ferramentaAtual == Ferramenta.Borracha)
                pontoFinal = null;
        }



        private void AtualizarRetangulo(Point mouseLocation)
        {
            if (!pontoInicial.HasValue)
                return;

            var offsetX = (panelCanvas.ClientSize.Width - GridWidth * zoom) / 2;
            var offsetY = (panelCanvas.ClientSize.Height - GridHeight * zoom) / 2;

            int x = (int)((mouseLocation.X - offsetX) / zoom);
            int y = (int)((mouseLocation.Y - offsetY) / zoom);

            // Evita desenhar fora dos limites
            if (x < 0 || x >= GridWidth || y < 0 || y >= GridHeight)
                return;

            pontoFinal = new Point(x, y);

            // Cria uma cópia temporária do bitmap para o preview
            if (previewBitmap == null)
                previewBitmap = (Bitmap)canvasBitmap.Clone();

            Bitmap temp = (Bitmap)previewBitmap.Clone();
            using (Graphics g = Graphics.FromImage(temp))
            using (Pen pen = new Pen(drawColor))
            {
                int x1 = Math.Min(pontoInicial.Value.X, pontoFinal.Value.X);
                int y1 = Math.Min(pontoInicial.Value.Y, pontoFinal.Value.Y);
                int x2 = Math.Max(pontoInicial.Value.X, pontoFinal.Value.X);
                int y2 = Math.Max(pontoInicial.Value.Y, pontoFinal.Value.Y);

                g.DrawRectangle(pen, x1, y1, x2 - x1, y2 - y1);
            }

            // Mostra o preview
            panelCanvas.BackgroundImage = new Bitmap(temp);
            panelCanvas.BackgroundImageLayout = ImageLayout.None;
        }
        private void DesenharCirculo(Point centro, Point borda, Color cor)
        {
            int radius = (int)Math.Round(Math.Sqrt(
                Math.Pow(borda.X - centro.X, 2) + Math.Pow(borda.Y - centro.Y, 2)
            ));

            int x = 0;
            int y = radius;
            int d = 3 - 2 * radius;

            while (y >= x)
            {
                // Oito octantes
                SetPixelSeguro(centro.X + x, centro.Y + y, cor);
                SetPixelSeguro(centro.X - x, centro.Y + y, cor);
                SetPixelSeguro(centro.X + x, centro.Y - y, cor);
                SetPixelSeguro(centro.X - x, centro.Y - y, cor);
                SetPixelSeguro(centro.X + y, centro.Y + x, cor);
                SetPixelSeguro(centro.X - y, centro.Y + x, cor);
                SetPixelSeguro(centro.X + y, centro.Y - x, cor);
                SetPixelSeguro(centro.X - y, centro.Y - x, cor);

                x++;
                if (d > 0)
                {
                    y--;
                    d = d + 4 * (x - y) + 10;
                }
                else
                {
                    d = d + 4 * x + 6;
                }
            }
        }

        // Função auxiliar para evitar exceções de limite
        private void SetPixelSeguro(int x, int y, Color cor)
        {
            if (x >= 0 && x < GridWidth && y >= 0 && y < GridHeight)
                canvasBitmap.SetPixel(x, y, cor);
        }


        private void DesenharRetangulo(Point inicio, Point fim, Color cor)
        {
            int x1 = Math.Min(inicio.X, fim.X);
            int x2 = Math.Max(inicio.X, fim.X);
            int y1 = Math.Min(inicio.Y, fim.Y);
            int y2 = Math.Max(inicio.Y, fim.Y);

            for (int x = x1; x <= x2; x++)
            {
                if (y1 >= 0 && y1 < GridHeight) canvasBitmap.SetPixel(x, y1, cor);
                if (y2 >= 0 && y2 < GridHeight) canvasBitmap.SetPixel(x, y2, cor);
            }
            for (int y = y1; y <= y2; y++)
            {
                if (x1 >= 0 && x1 < GridWidth) canvasBitmap.SetPixel(x1, y, cor);
                if (x2 >= 0 && x2 < GridWidth) canvasBitmap.SetPixel(x2, y, cor);
            }
        }


        private void PanelCanvas_MouseWheel(object sender, MouseEventArgs e)
        {
            zoom = e.Delta > 0 ? zoom * zoomIncrement : zoom / zoomIncrement;
            panelCanvas.Invalidate();
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add) zoom *= zoomIncrement;
            else if (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract) zoom /= zoomIncrement;

            if (e.Control && e.KeyCode == Keys.Z && undoStack.Count > 0)
            {
                canvasBitmap = undoStack.Pop();
                panelCanvas.Invalidate();
            }

            panelCanvas.Invalidate();
        }

        private void DrawPixel(Point mouseLocation, MouseButtons botao = MouseButtons.Left)
        {
            int x = (int)((mouseLocation.X - OffsetX) / zoom);
            int y = (int)((mouseLocation.Y - OffsetY) / zoom);
            if (x < 0 || x >= GridWidth || y < 0 || y >= GridHeight) return;

            Color corAtual = (botao == MouseButtons.Left) ? drawColor : secondaryColor;

            if (ferramentaAtual == Ferramenta.Balde)
            {
                Color corOriginal = canvasBitmap.GetPixel(x, y);
                PreencherArea(x, y, corOriginal, corAtual);
            }
            else
            {
                if (ferramentaAtual == Ferramenta.Borracha)
                    corAtual = Color.FromArgb(0, 0, 0, 0);

                Point currentPoint = new Point(x, y);

                if (pontoFinal == null)
                    canvasBitmap.SetPixel(currentPoint.X, currentPoint.Y, corAtual);
                else
                    DrawLineOnBitmap(pontoFinal.Value, currentPoint, corAtual);

                pontoFinal = currentPoint;
            }

            panelCanvas.Invalidate();
        }


        private void DrawLineOnBitmap(Point p1, Point p2, Color cor)
        {
            int dx = Math.Abs(p2.X - p1.X), dy = Math.Abs(p2.Y - p1.Y);
            int sx = p1.X < p2.X ? 1 : -1, sy = p1.Y < p2.Y ? 1 : -1;
            int err = dx - dy;
            int x0 = p1.X, y0 = p1.Y;

            while (true)
            {
                if (x0 >= 0 && x0 < GridWidth && y0 >= 0 && y0 < GridHeight)
                    canvasBitmap.SetPixel(x0, y0, cor);
                if (x0 == p2.X && y0 == p2.Y) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
        }

        private void PreencherArea(int startX, int startY, Color corOriginal, Color corNova)
        {
            if (corOriginal.ToArgb() == corNova.ToArgb()) return;

            Queue<Point> fila = new Queue<Point>();
            fila.Enqueue(new Point(startX, startY));

            while (fila.Count > 0)
            {
                Point p = fila.Dequeue();
                if (p.X < 0 || p.X >= GridWidth || p.Y < 0 || p.Y >= GridHeight) continue;
                if (canvasBitmap.GetPixel(p.X, p.Y) != corOriginal) continue;

                canvasBitmap.SetPixel(p.X, p.Y, corNova);

                fila.Enqueue(new Point(p.X + 1, p.Y));
                fila.Enqueue(new Point(p.X - 1, p.Y));
                fila.Enqueue(new Point(p.X, p.Y + 1));
                fila.Enqueue(new Point(p.X, p.Y - 1));
            }

            panelCanvas.Invalidate();
        }

        private void DrawPreviewPixel(Graphics g, int x, int y, Color cor)
        {
            if (x >= 0 && x < GridWidth && y >= 0 && y < GridHeight)
                g.FillRectangle(new SolidBrush(cor), OffsetX + x * zoom, OffsetY + y * zoom, zoom, zoom);
        }
        private void PanelCanvas_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            var offsetX = (panelCanvas.ClientSize.Width - GridWidth * zoom) / 2;
            var offsetY = (panelCanvas.ClientSize.Height - GridHeight * zoom) / 2;

            // ==================== Fundo quadriculado ====================
            int tamanhoQuad = (int)zoom / 2;
            using (Brush lightBrush = new SolidBrush(Color.FromArgb(180, 180, 180)))
            using (Brush darkBrush = new SolidBrush(Color.FromArgb(220, 220, 220)))
            {
                for (int y = 0; y < GridHeight; y++)
                {
                    for (int x = 0; x < GridWidth; x++)
                    {
                        g.FillRectangle(((x + y) % 2 == 0) ? lightBrush : darkBrush,
                            offsetX + x * zoom,
                            offsetY + y * zoom,
                            zoom, zoom);
                    }
                }
            }

            // ==================== Desenha pixels ====================
            for (int y = 0; y < GridHeight; y++)
            {
                for (int x = 0; x < GridWidth; x++)
                {
                    Color c = canvasBitmap.GetPixel(x, y);
                    if (c.A > 0)
                    {
                        using (Brush brush = new SolidBrush(c))
                            g.FillRectangle(brush, offsetX + x * zoom, offsetY + y * zoom, zoom, zoom);
                    }
                }
            }

            // ==================== Grid ====================
            using (Pen pen = new Pen(Color.FromArgb(100, Color.Black)))
            {
                for (int x = 0; x <= GridWidth; x++)
                    g.DrawLine(pen, offsetX + x * zoom, offsetY, offsetX + x * zoom, offsetY + GridHeight * zoom);
                for (int y = 0; y <= GridHeight; y++)
                    g.DrawLine(pen, offsetX, offsetY + y * zoom, offsetX + GridWidth * zoom, offsetY + y * zoom);
            }

            // ==================== Preview do retângulo ====================
            if (ferramentaAtual == Ferramenta.Retangulo && pontoInicial.HasValue)
            {
                int mouseX = (int)((MousePositionCanvas.X - OffsetX) / zoom);
                int mouseY = (int)((MousePositionCanvas.Y - OffsetY) / zoom);

                int x1 = Math.Min(pontoInicial.Value.X, mouseX);
                int x2 = Math.Max(pontoInicial.Value.X, mouseX);
                int y1 = Math.Min(pontoInicial.Value.Y, mouseY);
                int y2 = Math.Max(pontoInicial.Value.Y, mouseY);

                using (Brush previewBrush = new SolidBrush(Color.FromArgb(200, drawColor)))
                {
                    // Linha superior e inferior
                    for (int x = x1; x <= x2; x++)
                    {
                        g.FillRectangle(previewBrush, OffsetX + x * zoom, OffsetY + y1 * zoom, zoom, zoom);
                        g.FillRectangle(previewBrush, OffsetX + x * zoom, OffsetY + y2 * zoom, zoom, zoom);
                    }

                    // Linha esquerda e direita
                    for (int y = y1; y <= y2; y++)
                    {
                        g.FillRectangle(previewBrush, OffsetX + x1 * zoom, OffsetY + y * zoom, zoom, zoom);
                        g.FillRectangle(previewBrush, OffsetX + x2 * zoom, OffsetY + y * zoom, zoom, zoom);
                    }
                }
            }
            // ==================== Preview do círculo ====================
            if (ferramentaAtual == Ferramenta.Circulo && pontoInicial.HasValue)
            {
                int mouseX = (int)((MousePositionCanvas.X - OffsetX) / zoom);
                int mouseY = (int)((MousePositionCanvas.Y - OffsetY) / zoom);
                Point mousePoint = new Point(
                    Math.Max(0, Math.Min(GridWidth - 1, mouseX)),
                    Math.Max(0, Math.Min(GridHeight - 1, mouseY))
                );

                int radius = (int)Math.Round(Math.Sqrt(
                    Math.Pow(mousePoint.X - pontoInicial.Value.X, 2) +
                    Math.Pow(mousePoint.Y - pontoInicial.Value.Y, 2)
                ));

                int x = 0;
                int y = radius;
                int d = 3 - 2 * radius;

                Color corPreview = Color.FromArgb(200, drawColor);

                while (y >= x)
                {
                    DrawPreviewPixel(g, pontoInicial.Value.X + x, pontoInicial.Value.Y + y, corPreview);
                    DrawPreviewPixel(g, pontoInicial.Value.X - x, pontoInicial.Value.Y + y, corPreview);
                    DrawPreviewPixel(g, pontoInicial.Value.X + x, pontoInicial.Value.Y - y, corPreview);
                    DrawPreviewPixel(g, pontoInicial.Value.X - x, pontoInicial.Value.Y - y, corPreview);
                    DrawPreviewPixel(g, pontoInicial.Value.X + y, pontoInicial.Value.Y + x, corPreview);
                    DrawPreviewPixel(g, pontoInicial.Value.X - y, pontoInicial.Value.Y + x, corPreview);
                    DrawPreviewPixel(g, pontoInicial.Value.X + y, pontoInicial.Value.Y - x, corPreview);
                    DrawPreviewPixel(g, pontoInicial.Value.X - y, pontoInicial.Value.Y - x, corPreview);

                    x++;
                    if (d > 0)
                    {
                        y--;
                        d += 4 * (x - y) + 10;
                    }
                    else
                    {
                        d += 4 * x + 6;
                    }
                }
            }
        }

    }
}
