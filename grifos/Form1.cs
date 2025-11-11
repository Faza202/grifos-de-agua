using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Grifos
{
    // Grafo con lista de adyacencia (orden determinista con SortedSet)
    internal class Graph //clase 
    {
        private readonly Dictionary<string, SortedSet<string>> _adj = new(StringComparer.Ordinal);

        public void AddVertex(string v) //metodo para agregar vertices
        {
            v = v?.Trim() ?? string.Empty; //si v (vertice) es nulo, asignar cadena vacia
            if (v.Length == 0) return; //si v (vertice) es cadena vacia, salir del metodo
            if (!_adj.ContainsKey(v)) //si el diccionario no contiene la clave v (vertice), se asegura que no hayan 2 en el mismo sitio
                _adj[v] = new SortedSet<string>(StringComparer.Ordinal); //entonces agregar v (vertice) con un nuevo conjunto ordenado
        }

        public void AddEdge(string from, string to, bool undirected = true) //añadir aristas
        {
            from = from?.Trim() ?? string.Empty; //si from (desde) es nulo, asignar cadena vacia
            to = to?.Trim() ?? string.Empty; //si to (hasta) es nulo, asignar cadena vacia
            if (from.Length == 0 || to.Length == 0) return; //si from (desde) o to (hasta) es cadena vacia, salir del metodo

            AddVertex(from); //asegurar que el vertice from (desde) exista
            AddVertex(to); //asegurar que el vertice to (hasta) exista
            _adj[from].Add(to); //agregar to (hasta) a la lista de adyacencia de from (desde)
            if (undirected && from != to) //si es no dirigido y from (desde) es diferente de to (hasta)
                _adj[to].Add(from); //agregar from (desde) a la lista de adyacencia de to (hasta)
        }

        public void Clear() => _adj.Clear(); //si se quiere limpiar el grafo

        public IEnumerable<string> Vertices() => _adj.Keys.OrderBy(k => k, StringComparer.Ordinal); //obtener los vertices ordenados

        public IReadOnlyDictionary<string, SortedSet<string>> Adjacency => _adj; //obtener la lista de adyacencia

        public List<string> BFS(string start) //metodo para recorrido BFS, se usa lista para poder almacenar el resultado
        {
            var resultado = new List<string>(); //inicializar la lista de resultado
            if (string.IsNullOrWhiteSpace(start) || !_adj.ContainsKey(start)) return resultado; //si start (inicio) es nulo, vacio o no existe en el diccionario, retornar lista vacia

            var visitados = new HashSet<string>(StringComparer.Ordinal);
            var cola = new Queue<string>();
            visitados.Add(start);
            cola.Enqueue(start);

            while (cola.Count > 0) // mientras la cola tenga elementos
            {
                var v = cola.Dequeue(); //sacar el primer elemento de la cola
                resultado.Add(v); //agregar v (vertice) al resultado
                foreach (var vecino in _adj[v])
                {
                    if (!visitados.Contains(vecino)) //si el vecino no ha sido visitado
                    {
                        visitados.Add(vecino); //marcar el vecino como visitado
                        cola.Enqueue(vecino); //agregar el vecino a la cola
                    }
                }
            }

            return resultado;
        }

        public List<string> DFS(string start) //metodo para recorrido DFS, se usa lista para poder almacenar el resultado
        {
            var resultado = new List<string>(); //inicializar la lista de resultado
            if (string.IsNullOrWhiteSpace(start) || !_adj.ContainsKey(start)) return resultado;

            var visitados = new HashSet<string>(StringComparer.Ordinal);// conjunto para rastrear nodos visitados
            void Visitar(string v) //funcion recursiva para visitar nodos
            {
                visitados.Add(v); //marcar v (vertice) como visitado
                resultado.Add(v); //añadir al resultado
                foreach (var vecino in _adj[v])
                {
                    if (!visitados.Contains(vecino))
                        Visitar(vecino);
                }
            }

            Visitar(start);
            return resultado;
        }

        public string GetAdjacencyListFormatted()
        {
            var sb = new StringBuilder();
            foreach (var kvp in _adj.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                sb.Append(kvp.Key)
                  .Append(": ")
                  .Append(string.Join(", ", kvp.Value))
                  .AppendLine();
            }
            return sb.ToString();
        }
    }

    // Lienzo de dibujo: dibuja nodos como círculos, aristas como líneas, permite arrastrar nodos
    internal class GraphCanvas : Panel
    {
        public float NodeRadius { get; set; } = 22f;
        public Graph? Graph { get; private set; }
        public Dictionary<string, PointF> Positions { get; private set; } = new(StringComparer.Ordinal);

        private string? _dragNode;
        private PointF _dragOffset;
        private string? _hoverNode;
        private readonly Random _rand = new(Environment.TickCount);

        public GraphCanvas() //función para crear el canvas
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            Cursor = Cursors.Default;

            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            Resize += (s, e) => { ClampAll(); Invalidate(); };
        }

        public void SetData(Graph graph, Dictionary<string, PointF> positions) //función para establecer datos
        {
            Graph = graph;
            Positions = positions;
            EnsurePositions();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Graph is null) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            EnsurePositions();

            // Dibujar aristas (líneas y flechas para aristas dirigidas)
            using var penEdge = new Pen(Color.FromArgb(120, 120, 120), 2f);
            using var brushArrow = new SolidBrush(Color.FromArgb(120, 120, 120));

            var adj = Graph.Adjacency;
            var drawnUndirected = new HashSet<string>(StringComparer.Ordinal);

            foreach (var (from, tos) in adj)
            {
                foreach (var to in tos)
                {
                    if (!Positions.TryGetValue(from, out var p1) || !Positions.TryGetValue(to, out var p2))
                        continue;

                    var hasReverse = adj.TryGetValue(to, out var revs) && revs.Contains(from);
                    if (hasReverse)
                    {
                        // Evitar dibujar dos veces una arista no dirigida (cuando existen ambas direcciones)
                        var key = NormalizePair(from, to);
                        if (drawnUndirected.Contains(key)) continue;
                        drawnUndirected.Add(key);

                        DrawEdge(g, penEdge, p1, p2, directed: false);
                    }
                    else
                    {
                        // Arista dirigida: dibujar flecha hacia 'to'
                        DrawEdge(g, penEdge, p1, p2, directed: true, arrowBrush: brushArrow);
                    }
                }
            }

            // Dibujar nodos (círculos con texto)
            foreach (var v in Graph.Vertices())
            {
                if (!Positions.TryGetValue(v, out var center)) continue;
                var isHover = string.Equals(v, _hoverNode, StringComparison.Ordinal);
                var isDrag = string.Equals(v, _dragNode, StringComparison.Ordinal);

                var fill = isDrag ? Color.FromArgb(36, 110, 210)
                          : isHover ? Color.FromArgb(45, 125, 230)
                          : Color.FromArgb(32, 81, 152);
                var border = Color.FromArgb(20, 54, 102);

                var rect = CircleBounds(center, NodeRadius);

                using var path = RoundedCircle(rect);
                using var brush = new SolidBrush(fill);
                using var pen = new Pen(border, 2f);

                g.FillPath(brush, path);
                g.DrawPath(pen, path);

                // Texto centrado
                var textRect = Rectangle.Round(rect);
                TextRenderer.DrawText(g, v, new Font("Segoe UI", 9, FontStyle.Bold), textRect, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private void DrawEdge(Graphics g, Pen pen, PointF a, PointF b, bool directed, Brush? arrowBrush = null) //función para dibujar las esquinas
        {
            var dir = new PointF(b.X - a.X, b.Y - a.Y); //calcula la dirección de la línea
            var len = MathF.Sqrt(dir.X * dir.X + dir.Y * dir.Y); //calcúla 
            if (len < 1f) return;
            dir = new PointF(dir.X / len, dir.Y / len);

            var start = new PointF(a.X + dir.X * NodeRadius, a.Y + dir.Y * NodeRadius);
            var end = new PointF(b.X - dir.X * NodeRadius, b.Y - dir.Y * NodeRadius);

            g.DrawLine(pen, start, end);

            if (directed && arrowBrush is not null)
            {
                DrawArrowHead(g, arrowBrush, end, dir, size: 9f, angleDeg: 27f);
            }
        }

        private static void DrawArrowHead(Graphics g, Brush brush, PointF tip, PointF dir, float size, float angleDeg) //dubijar flecha
        {
            // Base del triángulo en sentido contrario a 'dir'
            var angleRad = angleDeg * (MathF.PI / 180f);

            // Rotación de dir
            var left = Rotate(dir, +angleRad);
            var right = Rotate(dir, -angleRad);

            var p1 = tip;
            var p2 = new PointF(tip.X - left.X * size, tip.Y - left.Y * size);
            var p3 = new PointF(tip.X - right.X * size, tip.Y - right.Y * size);

            g.FillPolygon(brush, new[] { p1, p2, p3 });
        }

        private static PointF Rotate(PointF v, float rad)
        {
            var c = MathF.Cos(rad);
            var s = MathF.Sin(rad);
            return new PointF(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }

        private RectangleF CircleBounds(PointF center, float r)
            => new RectangleF(center.X - r, center.Y - r, r * 2, r * 2);

        private static GraphicsPath RoundedCircle(RectangleF rect)
        {
            var path = new GraphicsPath();
            path.AddEllipse(rect);
            return path;
        }

        private static string NormalizePair(string a, string b)
            => string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";

        private void OnMouseDown(object? sender, MouseEventArgs e)
        {
            if (Graph is null) return;
            var hit = HitTest(e.Location);
            if (hit is null) return;

            _dragNode = hit;
            var center = Positions[hit];
            _dragOffset = new PointF(center.X - e.X, center.Y - e.Y);
            Invalidate();
        }

        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            if (Graph is null) return;

            if (_dragNode is not null && e.Button == MouseButtons.Left)
            {
                var newPos = new PointF(e.X + _dragOffset.X, e.Y + _dragOffset.Y);
                newPos = ClampToBounds(newPos);
                Positions[_dragNode] = newPos;
                Invalidate();
                return;
            }

            var prevHover = _hoverNode;
            _hoverNode = HitTest(e.Location);
            if (!string.Equals(prevHover, _hoverNode, StringComparison.Ordinal))
            {
                Cursor = _hoverNode is null ? Cursors.Default : Cursors.Hand;
                Invalidate();
            }
        }

        private void OnMouseUp(object? sender, MouseEventArgs e)
        {
            _dragNode = null;
        }

        private string? HitTest(Point p)
        {
            if (Graph is null) return null;
            foreach (var v in Graph.Vertices())
            {
                if (!Positions.TryGetValue(v, out var c)) continue;
                var dx = p.X - c.X;
                var dy = p.Y - c.Y;
                if (dx * dx + dy * dy <= NodeRadius * NodeRadius)
                    return v;
            }
            return null;
        }

        private void EnsurePositions()
        {
            if (Graph is null) return;
            foreach (var v in Graph.Vertices())
            {
                if (!Positions.ContainsKey(v))
                    Positions[v] = RandomPoint();
            }
        }

        private void ClampAll()
        {
            var keys = Positions.Keys.ToList();
            foreach (var k in keys)
                Positions[k] = ClampToBounds(Positions[k]);
        }

        private PointF ClampToBounds(PointF p)
        {
            var pad = NodeRadius + 6f;
            var minX = pad;
            var minY = pad;
            var maxX = Math.Max(pad, ClientSize.Width - pad);
            var maxY = Math.Max(pad, ClientSize.Height - pad);
            return new PointF(
                MathF.Min(maxX, MathF.Max(minX, p.X)),
                MathF.Min(maxY, MathF.Max(minY, p.Y))
            );
        }

        private PointF RandomPoint()
        {
            var pad = NodeRadius + 8f;
            var w = Math.Max(1, ClientSize.Width - (int)(pad * 2));
            var h = Math.Max(1, ClientSize.Height - (int)(pad * 2));
            var x = pad + (float)_rand.NextDouble() * w;
            var y = pad + (float)_rand.NextDouble() * h;
            return new PointF(x, y);
        }
    }

    public partial class Form1 : Form
    {
        private Graph _graph = null!;
        private readonly Dictionary<string, PointF> _positions = new(StringComparer.Ordinal);
        private readonly Random _rand = new(Environment.TickCount);

        // Controles UI
        private TableLayoutPanel _root = null!;
        private TableLayoutPanel _left = null!;
        private TableLayoutPanel _right = null!;
        private GraphCanvas _canvas = null!;
        private RichTextBox _rtbOut = null!;
        private ComboBox _cmbStart = null!;
        private TextBox _txtVertex = null!;
        private TextBox _txtFrom = null!;
        private TextBox _txtTo = null!;
        private CheckBox _chkUndirected = null!;

        private Button _btnAddVertex = null!;
        private Button _btnAddEdge = null!;
        private Button _btnBfs = null!;
        private Button _btnDfs = null!;
        private Button _btnAdj = null!;
        private Button _btnReset = null!;
        private Button _btnSample = null!;
        private Button _btnAutoLayout = null!;

        public Form1()
        {
            InitializeComponent();
            Text = "Grafo interactivo - BFS y DFS";
            MinimumSize = new Size(1080, 700);
            StartPosition = FormStartPosition.CenterScreen;

            BuildUI();

            Load += Form1_Load;
        }

        private void Form1_Load(object? sender, EventArgs e)
        {
            _graph = new Graph();
            LoadSampleGraph();
            RebuildPositionsCircle();
            _canvas.SetData(_graph, _positions);

            PrintHeader();
            PrintAdjacency();
            PrintTraversals("A");
            PrintDiff();
        }

        // Construcción UI
        private void BuildUI()
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.FromArgb(32, 81, 152)
            };
            var lblTitle = new Label
            {
                Text = "Editor de Grafos · BFS y DFS",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(16, 14)
            };
            header.Controls.Add(lblTitle);
            Controls.Add(header);

            _root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(8),
            };
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 400));
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(_root);

            // Panel izquierdo (controles)
            _left = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(4),
            };
            _left.RowStyles.Add(new RowStyle(SizeType.Absolute, 150)); // Nodos
            _left.RowStyles.Add(new RowStyle(SizeType.Absolute, 180)); // Aristas
            _left.RowStyles.Add(new RowStyle(SizeType.Absolute, 130)); // Recorridos
            _left.RowStyles.Add(new RowStyle(SizeType.Absolute, 100)); // Acciones
            _left.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));  // Layout
            _left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Filler
            _root.Controls.Add(_left, 0, 0);

            // Panel derecho (canvas + salida)
            _right = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(4)
            };
            _right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _right.RowStyles.Add(new RowStyle(SizeType.Absolute, 200));
            _root.Controls.Add(_right, 1, 0);

            _canvas = new GraphCanvas { Dock = DockStyle.Fill, BackColor = Color.White };
            _right.Controls.Add(_canvas, 0, 0);

            _rtbOut = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10),
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            _right.Controls.Add(_rtbOut, 0, 1);

            // Grupo: Nodos
            var gbNodes = new GroupBox
            {
                Text = "Nodos",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            var pnlNodes = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                Padding = new Padding(8)
            };
            pnlNodes.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            pnlNodes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pnlNodes.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            gbNodes.Controls.Add(pnlNodes);

            pnlNodes.Controls.Add(new Label { Text = "Nuevo:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
            _txtVertex = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, MaxLength = 24 };
            pnlNodes.Controls.Add(_txtVertex, 1, 0);
            _btnAddVertex = PrimaryButton("Añadir nodo", OnAddVertex);
            pnlNodes.Controls.Add(_btnAddVertex, 2, 0);

            pnlNodes.Controls.Add(new Label { Text = "Inicio:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
            _cmbStart = new ComboBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            pnlNodes.Controls.Add(_cmbStart, 1, 1);
            _btnSample = SecondaryButton("Cargar ejemplo", OnLoadSample);
            pnlNodes.Controls.Add(_btnSample, 2, 1);

            // Grupo: Aristas
            var gbEdges = new GroupBox
            {
                Text = "Aristas",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            var pnlEdges = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 3,
                Padding = new Padding(8)
            };
            pnlEdges.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
            pnlEdges.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            pnlEdges.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
            pnlEdges.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            gbEdges.Controls.Add(pnlEdges);

            pnlEdges.Controls.Add(new Label { Text = "Desde:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
            _txtFrom = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, MaxLength = 24 };
            pnlEdges.Controls.Add(_txtFrom, 1, 0);

            pnlEdges.Controls.Add(new Label { Text = "Hasta:", Anchor = AnchorStyles.Left, AutoSize = true }, 2, 0);
            _txtTo = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, MaxLength = 24 };
            pnlEdges.Controls.Add(_txtTo, 3, 0);

            _chkUndirected = new CheckBox { Text = "No dirigido", Checked = true, Anchor = AnchorStyles.Left };
            pnlEdges.SetColumnSpan(_chkUndirected, 2);
            pnlEdges.Controls.Add(_chkUndirected, 0, 1);

            _btnAddEdge = PrimaryButton("Añadir arista", OnAddEdge);
            pnlEdges.SetColumnSpan(_btnAddEdge, 2);
            pnlEdges.Controls.Add(_btnAddEdge, 2, 1);

            pnlEdges.Controls.Add(new Label
            {
                Text = "Tip: el grafo es no ponderado. Aristas duplicadas se ignoran.",
                AutoSize = true,
                ForeColor = Color.DimGray,
                Anchor = AnchorStyles.Left
            }, 0, 2);
            pnlEdges.SetColumnSpan(pnlEdges.GetControlFromPosition(0, 2), 4);

            // Grupo: Recorridos
            var gbTraverse = new GroupBox
            {
                Text = "Recorridos",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            var pnlTrav = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(8),
                WrapContents = true
            };
            _btnBfs = PrimaryButton("BFS desde inicio", OnBfs);
            _btnDfs = PrimaryButton("DFS desde inicio", OnDfs);
            _btnAdj = SecondaryButton("Mostrar adyacencia", (s, e) => { PrintHeader(); PrintAdjacency(); });
            pnlTrav.Controls.Add(_btnBfs);
            pnlTrav.Controls.Add(_btnDfs);
            pnlTrav.Controls.Add(_btnAdj);
            gbTraverse.Controls.Add(pnlTrav);

            // Grupo: Acciones
            var gbActions = new GroupBox
            {
                Text = "Acciones",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            var pnlActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(8),
                WrapContents = true
            };
            _btnReset = DangerButton("Limpiar grafo", OnReset);
            _btnAutoLayout = SecondaryButton("Auto-ubicar", OnAutoLayout);
            pnlActions.Controls.Add(_btnReset);
            pnlActions.Controls.Add(_btnAutoLayout);
            gbActions.Controls.Add(pnlActions);

            // Agregar grupos al panel izquierdo
            _left.Controls.Add(gbNodes, 0, 0);
            _left.Controls.Add(gbEdges, 0, 1);
            _left.Controls.Add(gbTraverse, 0, 2);
            _left.Controls.Add(gbActions, 0, 3);

            // Teclas rápidas
            AcceptButton = _btnAddVertex; // Enter añade nodo si procede
        }

        // Factories de botones con estilos
        private Button PrimaryButton(string text, EventHandler onClick) => new Button
        {
            Text = text,
            AutoSize = true,
            BackColor = Color.FromArgb(32, 81, 152),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(8, 6, 8, 6),
            Margin = new Padding(6),
            Cursor = Cursors.Hand
        }.With(b => b.Click += onClick);

        private Button SecondaryButton(string text, EventHandler onClick) => new Button
        {
            Text = text,
            AutoSize = true,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(32, 81, 152),
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(8, 6, 8, 6),
            Margin = new Padding(6),
            Cursor = Cursors.Hand
        }.With(b => b.Click += onClick);

        private Button DangerButton(string text, EventHandler onClick) => new Button
        {
            Text = text,
            AutoSize = true,
            BackColor = Color.Firebrick,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(8, 6, 8, 6),
            Margin = new Padding(6),
            Cursor = Cursors.Hand
        }.With(b => b.Click += onClick);

        // Eventos
        private void OnAddVertex(object? sender, EventArgs e)
        {
            var v = _txtVertex.Text.Trim();
            if (v.Length == 0)
            {
                Toast("Ingrese un nombre de nodo.", isWarning: true);
                return;
            }
            _graph.AddVertex(v);
            PlaceNewVertex(v);
            RefreshStartCombo();
            _txtVertex.Clear();
            Toast($"Nodo agregado: {v}");
            PrintHeader();
            PrintAdjacency();
            _canvas.Invalidate();
        }

        private void OnAddEdge(object? sender, EventArgs e)
        {
            var from = _txtFrom.Text.Trim();
            var to = _txtTo.Text.Trim();
            if (from.Length == 0 || to.Length == 0)
            {
                Toast("Complete 'Desde' y 'Hasta'.", isWarning: true);
                return;
            }
            _graph.AddEdge(from, to, _chkUndirected.Checked);
            // Asegurar posiciones si los nodos son nuevos
            PlaceNewVertex(from);
            PlaceNewVertex(to);
            RefreshStartCombo();
            Toast(_chkUndirected.Checked
                ? $"Arista agregada: {from} - {to}"
                : $"Arista agregada: {from} -> {to}");
            PrintHeader();
            PrintAdjacency();
            _canvas.Invalidate();
        }

        private void OnBfs(object? sender, EventArgs e)
        {
            var start = _cmbStart.SelectedItem as string ?? _cmbStart.Text?.Trim() ?? string.Empty;
            if (start.Length == 0)
            {
                Toast("Seleccione nodo de inicio.", isWarning: true);
                return;
            }
            var bfs = _graph.BFS(start);
            PrintHeader();
            AppendLine("BFS desde " + start + ": " + string.Join(", ", bfs));
            PrintDiff();
        }

        private void OnDfs(object? sender, EventArgs e)
        {
            var start = _cmbStart.SelectedItem as string ?? _cmbStart.Text?.Trim() ?? string.Empty;
            if (start.Length == 0)
            {
                Toast("Seleccione nodo de inicio.", isWarning: true);
                return;
            }
            var dfs = _graph.DFS(start);
            PrintHeader();
            AppendLine("DFS desde " + start + ": " + string.Join(", ", dfs));
            PrintDiff();
        }

        private void OnReset(object? sender, EventArgs e)
        {
            if (MessageBox.Show("¿Desea limpiar el grafo?", "Confirmación",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _graph.Clear();
                _positions.Clear();
                _cmbStart.Items.Clear();
                _cmbStart.Text = string.Empty;
                _rtbOut.Clear();
                _txtFrom.Clear();
                _txtTo.Clear();
                _txtVertex.Clear();
                Toast("Grafo limpiado.");
                PrintHeader();
                _canvas.SetData(_graph, _positions);
            }
        }

        private void OnLoadSample(object? sender, EventArgs e)
        {
            LoadSampleGraph();
            RebuildPositionsCircle();
            _canvas.SetData(_graph, _positions);

            Toast("Ejemplo cargado: nodos A-E y aristas A-B, A-C, B-D, C-E, D-E.");
            PrintHeader();
            PrintAdjacency();
            PrintTraversals("A");
            PrintDiff();
        }

        private void OnAutoLayout(object? sender, EventArgs e)
        {
            RebuildPositionsCircle();
            _canvas.Invalidate();
        }

        // Lógica auxiliar
        private void LoadSampleGraph()
        {
            _graph.Clear();
            foreach (var v in new[] { "A", "B", "C", "D", "E" }) _graph.AddVertex(v);
            _graph.AddEdge("A", "B");
            _graph.AddEdge("A", "C");
            _graph.AddEdge("B", "D");
            _graph.AddEdge("C", "E");
            _graph.AddEdge("D", "E");
            RefreshStartCombo();
            SelectStartIfExists("A");
        }

        private void PlaceNewVertex(string v)
        {
            if (_positions.ContainsKey(v)) return;
            var pad = 36;
            var w = Math.Max(2 * pad, _canvas.ClientSize.Width - 2 * pad);
            var h = Math.Max(2 * pad, _canvas.ClientSize.Height - 2 * pad);
            var x = pad + _rand.Next(w);
            var y = pad + _rand.Next(h);
            _positions[v] = new PointF(x, y);
        }

        private void RebuildPositionsCircle()
        {
            _positions.Clear();
            var verts = _graph.Vertices().ToList();
            if (verts.Count == 0) return;

            var w = Math.Max(1, _canvas.ClientSize.Width);
            var h = Math.Max(1, _canvas.ClientSize.Height);

            var cx = w / 2f;
            var cy = h / 2f;
            var r = MathF.Min(w, h) * 0.35f;

            for (int i = 0; i < verts.Count; i++)
            {
                var angle = (float)(i * (2 * Math.PI / verts.Count));
                var x = cx + r * MathF.Cos(angle);
                var y = cy + r * MathF.Sin(angle);
                _positions[verts[i]] = new PointF(x, y);
            }
        }

        private void RefreshStartCombo()
        {
            var sel = _cmbStart.SelectedItem as string ?? _cmbStart.Text;
            _cmbStart.BeginUpdate();
            _cmbStart.Items.Clear();
            foreach (var v in _graph.Vertices()) _cmbStart.Items.Add(v);
            _cmbStart.EndUpdate();
            SelectStartIfExists(sel);
        }

        private void SelectStartIfExists(string? v)
        {
            if (string.IsNullOrWhiteSpace(v)) return;
            foreach (var item in _cmbStart.Items)
            {
                if (string.Equals(item as string, v, StringComparison.Ordinal))
                {
                    _cmbStart.SelectedItem = item;
                    return;
                }
            }
        }

        private void PrintHeader()
        {
            _rtbOut.Clear();
            AppendLine("== Grafo (no ponderado) ==");
            AppendLine(DateTime.Now.ToString("HH:mm:ss"));
            AppendLine(string.Empty);
        }

        private void PrintAdjacency()
        {
            AppendLine("Lista de adyacencia:");
            AppendLine(_graph.GetAdjacencyListFormatted());
        }

        private void PrintTraversals(string start)
        {
            var bfs = _graph.BFS(start);
            var dfs = _graph.DFS(start);
            AppendLine("BFS desde " + start + ": " + string.Join(", ", bfs));
            AppendLine("DFS desde " + start + ": " + string.Join(", ", dfs));
            AppendLine(string.Empty);
        }

        private void PrintDiff()
        {
            AppendLine("Diferencia BFS vs DFS:");
            AppendLine("• BFS recorre por niveles usando una cola; halla rutas mínimas en grafos no ponderados.");
            AppendLine("• DFS profundiza antes de retroceder (pila/recursión); útil para ciclos, componentes y backtracking.");
        }

        private void AppendLine(string text) => _rtbOut.AppendText(text + Environment.NewLine);

        private void Toast(string text, bool isWarning = false)
        {
            var icon = isWarning ? MessageBoxIcon.Warning : MessageBoxIcon.Information;
            MessageBox.Show(text, "Información", MessageBoxButtons.OK, icon);
        }
    }

    // Extensión utilitaria para inicialización fluida
    internal static class ControlExtensions
    {
        public static T With<T>(this T control, Action<T> action)
            where T : Control
        {
            action(control);
            return control;
        }
    }
}