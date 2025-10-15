using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Svg;
using System.Drawing;
using System.Windows.Forms;



namespace PixelArtEditor
{
    public partial class Form1 : Form
    {
        private static readonly Color FUNDOPADRAOBTN = Color.FromArgb(60, 60, 64);
        private static readonly Color MOUSEHOVERBTNCOLOR = Color.FromArgb(15, 62, 138);
        private static readonly Color BTNATIVO = Color.DarkCyan;
        private static readonly Color CORCANVAS1 = Color.FromArgb(150, 150, 150);
        private static readonly Color CORCANVAS2 = Color.FromArgb(145, 145, 145);
        private static readonly Color CORPREFORMA = Color.FromArgb(100, 0, 255, 255);

        private const int GridWidth = 32;
        private const int GridHeight = 32;
        private bool mostrarGrid = false; // Controla a exibição da grade

        private Bitmap canvasBitmap;
        private Color drawColor = Color.Black;
        private Color secondaryColor = Color.FromArgb(65, 84, 63);
        private Color colorTransparent = Color.Transparent;

        private Panel colorPrimaryPanel;
        private Panel colorSecondaryPanel;
        private Panel colorTransparentPanel;
        private Panel[] quickColorPanels = new Panel[15];

        private Stack<Bitmap> undoStack = new Stack<Bitmap>();
        private const int MaxUndo = 20;

        private bool isDrawing = false;
        private float zoom = 10f;
        private float zoomIncrement = 1.1f;
        private const float zoommaximo = 60f;

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
        private enum Ferramenta { Lapiz, Borracha, Balde, Retangulo, Circulo, Linha, ContaGotas, BaldeInteligente, Selecao }
        private Ferramenta ferramentaAtual = Ferramenta.Lapiz;
        private Point? pontoFinal = null;
        private Point? pontoInicial = null;
        private bool mouseDentroCanvas = false;
        private Button btnLapis;
        private Button btnBorracha;
        private Button btnBalde;
        private Bitmap previewBitmap = null;
        private Button btnSelecao;

        // Flags globais
        private Button btnEspelhoH;
        private Button btnEspelhoV;
        private Button btnEspelhoHV;
        private enum Espelho { Nenhum, Horizontal, Vertical, Ambos }
        private Espelho espelhoAtual = Espelho.Nenhum;

        private bool espelhoH = false;
        private bool espelhoV = false;

        // Inicializa o ToolTip
        private ToolTip toolTip;

        // ==================== Ferramenta de Seleção ====================
        private bool isSelecting = false; // Indica se o usuário está selecionando
        private Point? selectStart = null; // Ponto inicial da seleção
        private Point? selectEnd = null; // Ponto final da seleção
        private Bitmap selectionBitmap = null; // Bitmap da área selecionada
        private Bitmap selectedArea = null; // Área selecionada para mover
        private Point selectionOffset; // Deslocamento da seleção
        private bool isMovingSelection = false; // Indica se o usuário está movendo a seleção


        public Form1()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            var screen = Screen.PrimaryScreen.WorkingArea;
            this.Width = (int)(screen.Width * .9);
            this.Height = (int)(screen.Height * .9);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.KeyPreview = true;
             
            this.Text = mostrarGrid ? "Pixel Art Editor - Grid ON (Ctrl+G para alternar)" : "Pixel Art Editor - Grid OFF (Ctrl+G para alternar)";

            toolTip = new ToolTip();
            toolTip.AutoPopDelay = 5000;
            toolTip.InitialDelay = 500;
            toolTip.ReshowDelay = 100;
            toolTip.ShowAlways = true;

            canvasBitmap = new Bitmap(GridWidth, GridHeight);
            CriarLayout();




        }
        private void FerramentaSelecao_MouseDown(object sender, MouseEventArgs e)
        {
            int x = (int)((e.X - OffsetX) / zoom);
            int y = (int)((e.Y - OffsetY) / zoom);
            selectStart = new Point(x, y);
            selectEnd = null;
            isSelecting = true;
        }

        private void FerramentaSelecao_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isSelecting || !selectStart.HasValue) return;

            int x = (int)((e.X - OffsetX) / zoom);
            int y = (int)((e.Y - OffsetY) / zoom);
            selectEnd = new Point(x, y);
            panelCanvas.Invalidate();
        }

        private void FerramentaSelecao_MouseUp(object sender, MouseEventArgs e)
        {
            if (!selectStart.HasValue || !selectEnd.HasValue) return;

            int x1 = Math.Min(selectStart.Value.X, selectEnd.Value.X);
            int y1 = Math.Min(selectStart.Value.Y, selectEnd.Value.Y);
            int x2 = Math.Max(selectStart.Value.X, selectEnd.Value.X);
            int y2 = Math.Max(selectStart.Value.Y, selectEnd.Value.Y);

            int width = x2 - x1 + 1;
            int height = y2 - y1 + 1;

            selectionBitmap = new Bitmap(width, height);

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    selectionBitmap.SetPixel(x, y, canvasBitmap.GetPixel(x1 + x, y1 + y));

            isSelecting = false;
            panelCanvas.Invalidate();
        }
        private void SetButtonSvg(Button btn, string caminhoSvg, int largura = 48, int altura = 48)
        {
            try
            {
                SvgDocument svgDoc = SvgDocument.Open(caminhoSvg);
                Bitmap bmp = svgDoc.Draw(largura, altura); // cria bitmap do tamanho desejado
                btn.Image = bmp;
                btn.ImageAlign = ContentAlignment.MiddleLeft; // ajusta a posição da imagem
                btn.TextImageRelation = TextImageRelation.ImageBeforeText; // texto depois da imagem
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar SVG: {ex.Message}");
            }
        }
        private void CriarLayout()
        {
            // ==================== Painel esquerdo ====================
            panelLeft = new Panel { Dock = DockStyle.Left, Width = 120, BackColor = Color.FromArgb(45, 45, 48) };
            this.Controls.Add(panelLeft);

            CriarFerramentas();

            // Define botão ativo inicial
            SetActiveButton(btnLapis);
            ferramentaAtual = Ferramenta.Lapiz;


            // ==================== Painel direito ====================
            panelRight = new Panel { Dock = DockStyle.Right, Width = 200, BackColor = Color.FromArgb(45, 45, 48) };
            this.Controls.Add(panelRight);

            // Cor primária
            colorPrimaryPanel = CriarPainelCor(drawColor, 20, 20);
            colorPrimaryPanel.Click += (s, e) => EscolherCor(ref drawColor, colorPrimaryPanel);
            panelRight.Controls.Add(colorPrimaryPanel);

            // Cor secundária
            colorSecondaryPanel = CriarPainelCor(secondaryColor, 80, 20);
            colorSecondaryPanel.Click += (s, e) => EscolherCor(ref secondaryColor, colorSecondaryPanel);
            panelRight.Controls.Add(colorSecondaryPanel);

            // Cor transparente
            colorTransparentPanel = CriarPainelCor(colorTransparent, 140, 20);
            colorTransparentPanel.MouseDown += (s, e) =>
            {
                if (colorTransparentPanel.BackColor == Color.Transparent)
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        drawColor = Color.Transparent;
                        colorPrimaryPanel.BackColor = drawColor;
                    }
                    else if (e.Button == MouseButtons.Right)
                    {
                        secondaryColor = Color.Transparent;
                        colorSecondaryPanel.BackColor = secondaryColor;
                    }
                }
            };

            AddHoverCoresRapidas(colorTransparentPanel);
            AddHoverCoresRapidas(colorPrimaryPanel);
            AddHoverCoresRapidas(colorSecondaryPanel);

            panelRight.Controls.Add(colorTransparentPanel);


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
                AddHoverCoresRapidas(p);
                quickColorPanels[i] = p;

                p.MouseDown += (s, e) =>
                {
                    Panel panelClicado = s as Panel;
                    if (panelClicado.BackColor == Color.Transparent) return;

                    if (Control.ModifierKeys == Keys.Shift && e.Button == MouseButtons.Left)
                        RemoverCorRapida(panelClicado);
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

            // ==================== Botões de Espelho ====================

            // Define a posição logo abaixo da paleta
            int espelhoStartY = startY + ((15 / 5) * (panelSize + padding)) + 20; // abaixo da última linha da paleta

            int btnWidth = 50;
            int btnHeight = 30;
            int btnSpacing = 10;
            int startX = 20;

            // Botão Espelho Horizontal
            btnEspelhoH = new Button
            {
                Text = "H",
                Width = btnWidth,
                Height = btnHeight,
                Location = new Point(startX, espelhoStartY),
                BackColor = FUNDOPADRAOBTN,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnEspelhoH.FlatAppearance.BorderSize = 1;
            btnEspelhoH.Click += BtnEspelhoH_Click;

            // Botão Espelho Vertical
            btnEspelhoV = new Button
            {
                Text = "V",
                Width = btnWidth,
                Height = btnHeight,
                Location = new Point(startX + btnWidth + btnSpacing, espelhoStartY),
                BackColor = FUNDOPADRAOBTN,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnEspelhoV.FlatAppearance.BorderSize = 1;
            btnEspelhoV.Click += BtnEspelhoV_Click;

            // Botão Espelho HV
            btnEspelhoHV = new Button
            {
                Text = "HV",
                Width = btnWidth,
                Height = btnHeight,
                Location = new Point(startX + (btnWidth + btnSpacing) * 2, espelhoStartY),
                BackColor = FUNDOPADRAOBTN,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnEspelhoHV.FlatAppearance.BorderSize = 1;
            btnEspelhoHV.Click += BtnEspelhoHV_Click;

            // Adiciona ao painel direito
            panelRight.Controls.Add(btnEspelhoH);
            panelRight.Controls.Add(btnEspelhoV);
            panelRight.Controls.Add(btnEspelhoHV);

            AddHoverEffect(btnEspelhoH);
            AddHoverEffect(btnEspelhoV);
            AddHoverEffect(btnEspelhoHV);

            toolTip.SetToolTip(btnEspelhoH, "Ativa/Desativa Espelho Horizontal");
            toolTip.SetToolTip(btnEspelhoV, "Ativa/Desativa Espelho Vertical");
            toolTip.SetToolTip(btnEspelhoHV, "Ativa/Desativa Espelho Horizontal e Vertical");

            // ==================== Painel inferior ====================
            panelBottom = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Color.FromArgb(45, 45, 48) };
            this.Controls.Add(panelBottom);

            btnNovo = new Button
            {
                Text = "Novo",
                Width = 100,
                Height = 30,
                Location = new Point(10, 10),
                BackColor = FUNDOPADRAOBTN,
                ForeColor = Color.White,
                FlatAppearance = { BorderSize = 1 }
            };
            btnExportar = new Button
            {
                Text = "Exportar",
                Width = 100,
                Height = 30,
                Location = new Point(120, 10),
                BackColor = FUNDOPADRAOBTN,
                ForeColor = Color.White,
                FlatAppearance = { BorderSize = 1 }

            };
            panelBottom.Controls.AddRange(new Control[] { btnNovo, btnExportar });

            btnExportar.Click += btnExportar_Click;
            AddHoverEffect(btnExportar);
            AddHoverEffect(btnNovo);

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

        // ==================== Funçãos Exportar PNG ====================
        private void btnExportar_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "PNG Image|*.png";
                sfd.Title = "Exportar Canvas como PNG";
                sfd.FileName = "pixelart.png";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // Salva o bitmap diretamente como PNG
                        canvasBitmap.Save(sfd.FileName, System.Drawing.Imaging.ImageFormat.Png);
                        MessageBox.Show("Imagem exportada com sucesso!", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Erro ao exportar: " + ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        // ==================== Funções de espelho ====================
        private void SetActiveMirrorButton(Button botao)
        {
            // Se o botão clicado já representa o espelho ativo, desativa
            if ((botao == btnEspelhoH && espelhoAtual == Espelho.Horizontal) ||
                (botao == btnEspelhoV && espelhoAtual == Espelho.Vertical) ||
                (botao == btnEspelhoHV && espelhoAtual == Espelho.Ambos))
            {
                espelhoAtual = Espelho.Nenhum;
                btnEspelhoH.BackColor = FUNDOPADRAOBTN;
                btnEspelhoV.BackColor = FUNDOPADRAOBTN;
                btnEspelhoHV.BackColor = FUNDOPADRAOBTN;
                return;
            }

            // Define novo espelho ativo
            btnEspelhoH.BackColor = (botao == btnEspelhoH) ? BTNATIVO : FUNDOPADRAOBTN;
            btnEspelhoV.BackColor = (botao == btnEspelhoV) ? BTNATIVO : FUNDOPADRAOBTN;
            btnEspelhoHV.BackColor = (botao == btnEspelhoHV) ? BTNATIVO : FUNDOPADRAOBTN;

            if (botao == btnEspelhoH) espelhoAtual = Espelho.Horizontal;
            else if (botao == btnEspelhoV) espelhoAtual = Espelho.Vertical;
            else if (botao == btnEspelhoHV) espelhoAtual = Espelho.Ambos;
        }
        private void BtnEspelhoH_Click(object sender, EventArgs e)
        {
            espelhoH = true;
            espelhoV = false;
            SetActiveMirrorButton(btnEspelhoH);
        }

        private void BtnEspelhoV_Click(object sender, EventArgs e)
        {
            espelhoH = false;
            espelhoV = true;
            SetActiveMirrorButton(btnEspelhoV);
        }

        private void BtnEspelhoHV_Click(object sender, EventArgs e)
        {
            espelhoH = true;
            espelhoV = true;
            SetActiveMirrorButton(btnEspelhoHV);
        }

        private void SetPixelComEspelho(int x, int y, Color cor)
        {
            List<Point> pontos = new List<Point>();

            // Sempre o ponto original
            pontos.Add(new Point(x, y));

            // Calcula as coordenadas espelhadas
            int xEspelhado = GridWidth - 1 - x;
            int yEspelhado = GridHeight - 1 - y;

            // Espelho horizontal: espelha no eixo vertical (inverte X)
            if (espelhoH && !espelhoV)
            {
                pontos.Add(new Point(xEspelhado, y));
            }
            // Espelho vertical: espelha no eixo horizontal (inverte Y)
            else if (!espelhoH && espelhoV)
            {
                pontos.Add(new Point(x, yEspelhado));
            }
            // Espelho duplo: faz os dois
            else if (espelhoH && espelhoV)
            {
                pontos.Add(new Point(xEspelhado, y));      // horizontal
                pontos.Add(new Point(x, yEspelhado));      // vertical
                pontos.Add(new Point(xEspelhado, yEspelhado)); // ambos
            }

            // Desenha os pontos válidos
            foreach (var p in pontos)
            {
                if (p.X >= 0 && p.X < GridWidth && p.Y >= 0 && p.Y < GridHeight)
                    canvasBitmap.SetPixel(p.X, p.Y, cor);
            }
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
        // Adiciona a cor na paleta rápida se houver espaço
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
        // Remove a cor do painel clicado e desloca as cores à direita
        private void RemoverCorRapida(Panel panelClicado)
        {
            int index = Array.IndexOf(quickColorPanels, panelClicado);
            if (index == -1) return;

            // Remove a cor do painel clicado
            panelClicado.BackColor = Color.Transparent;

            // Desloca as cores à direita para preencher o espaço
            for (int i = index; i < quickColorPanels.Length - 1; i++)
            {
                quickColorPanels[i].BackColor = quickColorPanels[i + 1].BackColor;
            }

            // Último painel fica transparente
            quickColorPanels[quickColorPanels.Length - 1].BackColor = Color.Transparent;
        }

        // ===============================
        // 🧰 CRIAÇÃO DE FERRAMENTAS
        // ===============================

        private void CriarFerramentas()
        {
            // 🖌️ Criação dos botões de ferramentas com o enum correto
            btnLapis = CriarBotaoFerramenta("", 0, Ferramenta.Lapiz);
            btnBorracha = CriarBotaoFerramenta("", 1, Ferramenta.Borracha);
            btnBalde = CriarBotaoFerramenta("", 2, Ferramenta.Balde);
            Button btnBaldeInteligente = CriarBotaoFerramenta("", 3, Ferramenta.BaldeInteligente);
            Button btnRetangulo = CriarBotaoFerramenta("", 4, Ferramenta.Retangulo);
            Button btnCirculo = CriarBotaoFerramenta("", 5, Ferramenta.Circulo);
            Button btnLinha = CriarBotaoFerramenta("", 6, Ferramenta.Linha);
            Button btnContaGotas = CriarBotaoFerramenta("", 7, Ferramenta.ContaGotas);
            Button btnSelecao = CriarBotaoFerramenta("", 8, Ferramenta.Selecao);

            // Adiciona ao painel
            panelLeft.Controls.Add(btnLapis);
            panelLeft.Controls.Add(btnBorracha);
            panelLeft.Controls.Add(btnBalde);
            panelLeft.Controls.Add(btnBaldeInteligente);
            panelLeft.Controls.Add(btnRetangulo);
            panelLeft.Controls.Add(btnCirculo);
            panelLeft.Controls.Add(btnLinha);
            panelLeft.Controls.Add(btnContaGotas);
            panelLeft.Controls.Add(btnSelecao);

            // Ícones SVG (certifique-se de que os arquivos SVG estão no caminho correto)
            SetButtonSvg(btnLapis, "icons/lapis.svg");
            SetButtonSvg(btnBorracha, "icons/borracha.svg");
            SetButtonSvg(btnBalde, "icons/balde.svg");
            SetButtonSvg(btnRetangulo, "icons/retangulo.svg");
            SetButtonSvg(btnCirculo, "icons/circulo.svg");
            SetButtonSvg(btnLinha, "icons/linha.svg");
            SetButtonSvg(btnBaldeInteligente, "icons/baldeinteligente.svg");
            SetButtonSvg(btnContaGotas, "icons/conta-gotas.svg");

            toolTip.SetToolTip(btnLapis, "Lápis (Atalho: P)\nDesenhar pixel a pixel");
            toolTip.SetToolTip(btnBorracha, "Borracha (Atalho: E)\nApagar pixel");
            toolTip.SetToolTip(btnBalde, "Balde de Tinta (Atalho: B) \nPreencher áreas");
            toolTip.SetToolTip(btnBaldeInteligente, "Balde Inteligente (Atalho: Shift+B) \nPreencher todos os pixels de mesma cor");
            toolTip.SetToolTip(btnRetangulo, "Retângulo (Atalho: R)\nDesenhar retângulos");
            toolTip.SetToolTip(btnCirculo, "Círculo (Atalho: C)\nDesenhar círculos");
            toolTip.SetToolTip(btnLinha, "Linha (Atalho: L)\nDesenhar linhas retas");
            toolTip.SetToolTip(btnContaGotas, "Conta-Gotas (Atalho: I)\nSelecionar cor do pixel clicado");
            toolTip.SetToolTip(btnSelecao, "Seleção (Atalho: S)\nSelecionar área do canvas");

            // Define o botão ativo inicialmente
            SetActiveButton(btnLapis);
            ferramentaAtual = Ferramenta.Lapiz;
        }

        // ===============================
        // 🧱 MÉTODO DE CRIAÇÃO DE BOTÕES
        // ===============================

        private Button CriarBotaoFerramenta(string texto, int posY, Ferramenta ferramenta)
        {
            int btnSize = 60;
            int spacing = 10;


            Button btn = new Button
            {
                Text = texto,
                Width = 60,
                Height = btnSize,
                Location = new Point(30, posY * (btnSize + spacing) + 10),
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

        private void AddHoverCoresRapidas(Panel qc)
        {
            int originalSize = qc.Width;
            int hoverSize = (int)(originalSize * 1.2); // aumenta 20%
            Point originalLocation = qc.Location; // salva posição original

            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            timer.Interval = 15;

            int targetSize = originalSize;
            int step = 2;



            qc.MouseEnter += (s, e) =>
            {
                if (qc.BackColor == Color.Transparent)
                    return; // não aplica efeito se for transparente
                targetSize = hoverSize;
                timer.Start();
            };

            qc.MouseLeave += (s, e) =>
            {
                targetSize = originalSize;
                timer.Start();
            };

            timer.Tick += (s, e) =>
            {
                if (qc.Width < targetSize)
                {
                    qc.Width = Math.Min(qc.Width + step, targetSize);
                    qc.Height = qc.Width;
                }
                else if (qc.Width > targetSize)
                {
                    qc.Width = Math.Max(qc.Width - step, targetSize);
                    qc.Height = qc.Width;
                }
                else
                {
                    timer.Stop();
                }

                // Centraliza usando a posição original
                qc.Left = originalLocation.X - (qc.Width - originalSize) / 2;
                qc.Top = originalLocation.Y - (qc.Height - originalSize) / 2;
            };
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
            int x = (int)((e.X - OffsetX) / zoom);
            int y = (int)((e.Y - OffsetY) / zoom);
            if (x < 0 || x >= GridWidth || y < 0 || y >= GridHeight) return;

            // ----------------- Conta-gotas -----------------
            if (ferramentaAtual == Ferramenta.ContaGotas)
            {
                Color corClicada = canvasBitmap.GetPixel(x, y);

                if (e.Button == MouseButtons.Left)
                {
                    drawColor = corClicada;
                    colorPrimaryPanel.BackColor = drawColor;
                }
                else if (e.Button == MouseButtons.Right)
                {
                    secondaryColor = corClicada;
                    colorSecondaryPanel.BackColor = secondaryColor;
                }

                // Adiciona a cor na paleta rápida apenas se ainda não existir
                bool corExiste = false;
                foreach (var p in quickColorPanels)
                {
                    if (p.BackColor.ToArgb() == corClicada.ToArgb())
                    {
                        corExiste = true;
                        break;
                    }
                }

                if (!corExiste)
                    AdicionarCorRapida(corClicada);

                return; // não desenhar
            }


            // ----------------- Outras ferramentas -----------------
            if (e.Button != MouseButtons.Left && e.Button != MouseButtons.Right) return;

            // ----------------- Seleção -----------------
            if (ferramentaAtual == Ferramenta.Selecao)
            {
                if (e.Button == MouseButtons.Left)
                {
                    selectStart = new Point(x, y);
                    selectEnd = selectStart;
                    isSelecting = true;

                    panelCanvas.Invalidate(); // redesenha para mostrar seleção
                    return; // não desenha nada
                }
            }

            // Salva estado atual para undo
            SalvarParaUndo();


            // ----------------- Ferramentas de forma -----------------
            if (ferramentaAtual == Ferramenta.Retangulo || ferramentaAtual == Ferramenta.Circulo || ferramentaAtual == Ferramenta.Linha)
            {
                pontoInicial = new Point(x, y);
                pontoFinal = null;
            }
            else if (ferramentaAtual == Ferramenta.BaldeInteligente)
            {
                Color corAlvo = canvasBitmap.GetPixel(x, y);
                Color corNova = (e.Button == MouseButtons.Left) ? drawColor : secondaryColor;

                if (corAlvo != corNova)
                    BaldeInteligenteGlobal(corAlvo, corNova);
            }
            else
            {
                isDrawing = true;
                DrawPixel(new Point(x, y), e.Button);
            }

            panelCanvas.Invalidate();
        }

        private void PanelCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            MousePositionCanvas = e.Location;

            int x = (int)((e.X - OffsetX) / zoom);
            int y = (int)((e.Y - OffsetY) / zoom);
            x = Math.Max(0, Math.Min(GridWidth - 1, x));
            y = Math.Max(0, Math.Min(GridHeight - 1, y));

            if ((ferramentaAtual == Ferramenta.Retangulo || ferramentaAtual == Ferramenta.Circulo) && pontoInicial.HasValue)
            {
                pontoFinal = new Point(x, y);
                panelCanvas.Invalidate(); // redesenha o preview
            }
            else if (ferramentaAtual == Ferramenta.Linha && pontoInicial.HasValue)
            {
                pontoFinal = new Point(x, y);
                panelCanvas.Invalidate(); // força o redraw do canvas
            }
            else if (ferramentaAtual == Ferramenta.Selecao && isSelecting && selectStart.HasValue)
            {
                selectEnd = new Point(x, y);
                panelCanvas.Invalidate(); // redesenha para mostrar seleção
            }
            else if (isDrawing)
            {
                // 🔧 Corrigido: detectar qual botão está pressionado
                if (Control.MouseButtons == MouseButtons.Left)
                    DrawPixel(new Point(x, y), MouseButtons.Left);
                else if (Control.MouseButtons == MouseButtons.Right)
                    DrawPixel(new Point(x, y), MouseButtons.Right);
            }
        }

        private void PanelCanvas_MouseUp(object sender, MouseEventArgs e)
        {
            if ((ferramentaAtual == Ferramenta.Retangulo || ferramentaAtual == Ferramenta.Circulo) && pontoInicial.HasValue && pontoFinal.HasValue)
            {
                Color corAtual = (e.Button == MouseButtons.Left) ? drawColor : secondaryColor;

                if (ferramentaAtual == Ferramenta.Retangulo)
                    DesenharRetangulo(pontoInicial.Value, pontoFinal.Value, corAtual);
                else if (ferramentaAtual == Ferramenta.Circulo)
                    DesenharCirculo(pontoInicial.Value, pontoFinal.Value, corAtual);

                pontoInicial = null;
                pontoFinal = null;
                panelCanvas.Invalidate();
            }
            if (ferramentaAtual == Ferramenta.Linha && pontoInicial.HasValue && pontoFinal.HasValue)
            {
                Color corUsada = (e.Button == MouseButtons.Right) ? secondaryColor : drawColor;

                foreach (Point p in GetLinePoints(pontoInicial.Value.X, pontoInicial.Value.Y,
                                                 pontoFinal.Value.X, pontoFinal.Value.Y))
                {
                    SetPixelComEspelho(p.X, p.Y, corUsada);
                }

                pontoInicial = null;
                pontoFinal = null;
                panelCanvas.Invalidate();
            }
            if (isSelecting && selectStart.HasValue && selectEnd.HasValue)
            {
                isSelecting = false;
                // Aqui você pode copiar os pixels da seleção ou preparar para mover, se quiser
                panelCanvas.Invalidate();
            }

            isDrawing = false;

            if (ferramentaAtual == Ferramenta.Lapiz || ferramentaAtual == Ferramenta.Borracha)
                pontoFinal = null;
        }
        private IEnumerable<Point> GetLinePoints(int x0, int y0, int x1, int y1)
        {
            List<Point> pontos = new List<Point>();

            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                pontos.Add(new Point(x0, y0));
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }

            return pontos;
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
                SetPixelComEspelho(centro.X + x, centro.Y + y, cor);
                SetPixelComEspelho(centro.X - x, centro.Y + y, cor);
                SetPixelComEspelho(centro.X + x, centro.Y - y, cor);
                SetPixelComEspelho(centro.X - x, centro.Y - y, cor);
                SetPixelComEspelho(centro.X + y, centro.Y + x, cor);
                SetPixelComEspelho(centro.X - y, centro.Y + x, cor);
                SetPixelComEspelho(centro.X + y, centro.Y - x, cor);
                SetPixelComEspelho(centro.X - y, centro.Y - x, cor);

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
        private void DesenharRetangulo(Point inicio, Point fim, Color cor)
        {
            int x1 = Math.Min(inicio.X, fim.X);
            int x2 = Math.Max(inicio.X, fim.X);
            int y1 = Math.Min(inicio.Y, fim.Y);
            int y2 = Math.Max(inicio.Y, fim.Y);

            for (int x = x1; x <= x2; x++)
            {
                SetPixelComEspelho(x, y1, cor);
                SetPixelComEspelho(x, y2, cor);
            }
            for (int y = y1; y <= y2; y++)
            {
                SetPixelComEspelho(x1, y, cor);
                SetPixelComEspelho(x2, y, cor);
            }
        }

        private void PanelCanvas_MouseWheel(object sender, MouseEventArgs e)
        {
            float oldZoom = zoom;
            if (e.Delta > 0)
                zoom *= 1.1f;
            else
                zoom *= 0.9f;

            // 🔧 Limite de zoom
            zoom = Math.Max(1.0f, Math.Min(zoom, zoommaximo));

            // Reajuste opcional: manter o ponto central ao dar zoom
            //int mouseX = e.X;
            //int mouseY = e.Y;
            //OffsetX = mouseX - (int)((mouseX - OffsetX) * (zoom / oldZoom));
            //OffsetY = mouseY - (int)((mouseY - OffsetY) * (zoom / oldZoom));
            panelCanvas.Invalidate();
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e) // Atalhos
        {
            if (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add) zoom *= zoomIncrement;
            else if (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract) zoom /= zoomIncrement;

            if (e.Control && e.KeyCode == Keys.Z && undoStack.Count > 0)
            {
                canvasBitmap = undoStack.Pop();
                panelCanvas.Invalidate();
            }
            if (e.KeyCode == Keys.Escape)
            {
                selectStart = null;
                selectEnd = null;
                isSelecting = false;
                panelCanvas.Invalidate();
            }
            if (!e.Control && e.KeyCode == Keys.Z)
            {
                Color corTemp = drawColor;
                drawColor = secondaryColor;
                secondaryColor = corTemp;

                colorSecondaryPanel.BackColor = secondaryColor;
                colorPrimaryPanel.BackColor = drawColor;
            }
            if (e.Control && e.KeyCode == Keys.G)
            {
                mostrarGrid = !mostrarGrid;
            }


            panelCanvas.Invalidate();
        }
        private void BaldeInteligenteGlobal(Color corAlvo, Color corNova)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                for (int x = 0; x < GridWidth; x++)
                {
                    if (canvasBitmap.GetPixel(x, y) == corAlvo)
                        SetPixelComEspelho(x, y, corNova);
                }
            }

            panelCanvas.Invalidate();
        }


        private void DrawPixel(Point p, MouseButtons botao = MouseButtons.Left)
        {
            Color corAtual = (botao == MouseButtons.Left) ? drawColor : secondaryColor;

            if (ferramentaAtual == Ferramenta.Balde)
            {
                Color corOriginal = canvasBitmap.GetPixel(p.X, p.Y);
                PreencherArea(p.X, p.Y, corOriginal, corAtual);
            }
            else if (ferramentaAtual == Ferramenta.Borracha)
            {
                SetPixelComEspelho(p.X, p.Y, Color.Transparent); // Transparente
            }
            else // lápis ou desenho contínuo
            {
                if (pontoFinal == null)
                    SetPixelComEspelho(p.X, p.Y, corAtual);
                else
                    DrawLineOnBitmapComEspelho(pontoFinal.Value, p, corAtual);
            }

            pontoFinal = p;
            panelCanvas.Invalidate();
        }

        private void DrawLineOnBitmapComEspelho(Point p1, Point p2, Color cor)
        {
            int dx = Math.Abs(p2.X - p1.X), dy = Math.Abs(p2.Y - p1.Y);
            int sx = p1.X < p2.X ? 1 : -1, sy = p1.Y < p2.Y ? 1 : -1;
            int err = dx - dy;
            int x0 = p1.X, y0 = p1.Y;

            while (true)
            {
                SetPixelComEspelho(x0, y0, cor);
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
        private void DrawPreviewPixelComEspelho(Graphics g, int x, int y, Color cor)
        {
            List<Point> pontos = new List<Point>();
            pontos.Add(new Point(x, y));

            int xEsp = GridWidth - 1 - x;
            int yEsp = GridHeight - 1 - y;

            if (espelhoH && !espelhoV) // apenas horizontal
                pontos.Add(new Point(xEsp, y));
            else if (!espelhoH && espelhoV) // apenas vertical
                pontos.Add(new Point(x, yEsp));
            else if (espelhoH && espelhoV) // ambos
            {
                pontos.Add(new Point(xEsp, y));
                pontos.Add(new Point(x, yEsp));
                pontos.Add(new Point(xEsp, yEsp));
            }

            foreach (var p in pontos)
            {
                if (p.X >= 0 && p.X < GridWidth && p.Y >= 0 && p.Y < GridHeight)
                    DrawPreviewPixel(g, p.X, p.Y, cor);
            }
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
            using (Brush lightBrush = new SolidBrush(CORCANVAS1))
            using (Brush darkBrush = new SolidBrush(CORCANVAS2))
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
            if (mostrarGrid)
            {
                using (Pen pen = new Pen(Color.FromArgb(100, Color.Black)))
                {
                    for (int x = 0; x <= GridWidth; x++)
                        g.DrawLine(pen, offsetX + x * zoom, offsetY, offsetX + x * zoom, offsetY + GridHeight * zoom);
                    for (int y = 0; y <= GridHeight; y++)
                        g.DrawLine(pen, offsetX, offsetY + y * zoom, offsetX + GridWidth * zoom, offsetY + y * zoom);
                }
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

                using (Brush previewBrush = new SolidBrush(CORPREFORMA))
                {
                    // Linha superior e inferior
                    for (int x = x1; x <= x2; x++)
                    {
                        DrawPreviewPixelComEspelho(g, x, y1, CORPREFORMA);
                        DrawPreviewPixelComEspelho(g, x, y2, CORPREFORMA);
                    }

                    for (int y = y1; y <= y2; y++)
                    {
                        DrawPreviewPixelComEspelho(g, x1, y, CORPREFORMA);
                        DrawPreviewPixelComEspelho(g, x2, y, CORPREFORMA);
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


                while (y >= x)
                {
                    DrawPreviewPixelComEspelho(g, pontoInicial.Value.X + x, pontoInicial.Value.Y + y, CORPREFORMA);
                    DrawPreviewPixelComEspelho(g, pontoInicial.Value.X - x, pontoInicial.Value.Y + y, CORPREFORMA);
                    DrawPreviewPixelComEspelho(g, pontoInicial.Value.X + x, pontoInicial.Value.Y - y, CORPREFORMA);
                    DrawPreviewPixelComEspelho(g, pontoInicial.Value.X - x, pontoInicial.Value.Y - y, CORPREFORMA);
                    DrawPreviewPixelComEspelho(g, pontoInicial.Value.X + y, pontoInicial.Value.Y + x, CORPREFORMA);
                    DrawPreviewPixelComEspelho(g, pontoInicial.Value.X - y, pontoInicial.Value.Y + x, CORPREFORMA);
                    DrawPreviewPixelComEspelho(g, pontoInicial.Value.X + y, pontoInicial.Value.Y - x, CORPREFORMA);
                    DrawPreviewPixelComEspelho(g, pontoInicial.Value.X - y, pontoInicial.Value.Y - x, CORPREFORMA);


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
            // ==================== Preview da Linha ====================
            if (ferramentaAtual == Ferramenta.Linha && pontoInicial.HasValue && pontoFinal.HasValue)
            {
                Color corPreview = CORPREFORMA; // cor semi-transparente de preview
                foreach (Point p in GetLinePoints(pontoInicial.Value.X, pontoInicial.Value.Y, pontoFinal.Value.X, pontoFinal.Value.Y))
                    DrawPreviewPixelComEspelho(e.Graphics, p.X, p.Y, CORPREFORMA);

            }
            // ==================== Desenhar seleção (tracejado) ====================
            if (selectStart.HasValue && selectEnd.HasValue)
            {
                int x1 = Math.Min(selectStart.Value.X, selectEnd.Value.X);
                int y1 = Math.Min(selectStart.Value.Y, selectEnd.Value.Y);
                int x2 = Math.Max(selectStart.Value.X, selectEnd.Value.X);
                int y2 = Math.Max(selectStart.Value.Y, selectEnd.Value.Y);

                RectangleF rect = new RectangleF(OffsetX + x1 * zoom, OffsetY + y1 * zoom,
                                               (x2 - x1 + 1) * zoom, (y2 - y1 + 1) * zoom);
                using (Pen pen = new Pen(Color.White))
                {
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    g.DrawRectangle(pen, rect);
                }
            }

        }

    }
}
