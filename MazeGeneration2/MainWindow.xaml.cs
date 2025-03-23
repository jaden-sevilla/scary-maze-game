using System.ComponentModel;
using System.Data.Common;
using System.IO;
using System.Media;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Security.Policy;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using static System.Net.Mime.MediaTypeNames;

namespace MazeGeneration2
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private string size;
		private int countdownTime;
		private string projectPath = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName;
		private const bool jumpSound = true; // Because I don't want to be jumpscared every single time I test my program
		private int generationSpeed;
		private bool enableWin;
		private SoundPlayer tickPlayer;
		private SoundPlayer buildupPlayer;
		private DispatcherTimer dispatcherTimer = new DispatcherTimer();

		public MainWindow()
		{
			InitializeComponent();

			dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
			dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 1);

			Restart();
		}

		public void Restart()
		{
			MainMenu.Visibility = Visibility.Visible;
			SelectSizeGrid.Visibility = Visibility.Hidden;
			InstructionGrid.Visibility = Visibility.Hidden;
			PlayGrid.Visibility = Visibility.Hidden;
			EndGrid.Visibility = Visibility.Hidden;

			generationSpeed = 10;
			timer.Foreground = Brushes.White;
			enableWin = false;
		}

		public async void GenerateMaze(string size)
		{
			Random rand = new Random();
			bool[,] mazeArray;

			// Small - 15, 29
			// Medium - 25, 49
			// Large - 31, 61
			// Massive - 37,75
			if (size == "small")
			{
				mazeArray = new bool[15, 29];
			}
			else if (size == "medium")
			{
				mazeArray = new bool[23, 45];
			}
			else if (size == "large")
			{
				mazeArray = new bool[31, 61];
			}
			else
			{
				mazeArray = new bool[37, 75];
			}

			List<(int, int)> frontiers = new List<(int, int)>();

			InitialiseGrid(mazeArray);
			gameInfo.Text = "generating maze...";
			int startRow;
			int startColumn;
			do
			{
				startRow = rand.Next(0, mazeArray.GetLength(0));
			} while (startRow % 2 == 0);

			do
			{
				startColumn = rand.Next(0, mazeArray.GetLength(1));
			}
			while (startColumn % 2 == 0);

			mazeArray[startRow, startColumn] = true;
			UpdateCellColour(startRow, startColumn);
			GetFrontiers(startRow, startColumn, mazeArray, frontiers);

			while (frontiers.Count > 0)
			{
				int randIndex = rand.Next(0, frontiers.Count);
				(int, int) frontier = frontiers[randIndex];
				frontiers.RemoveAt(randIndex);
				(int, int) passage = FindPassage(frontier.Item1, frontier.Item2, mazeArray);
				(int, int) connection = ((frontier.Item1 + passage.Item1) / 2, (frontier.Item2 + passage.Item2) / 2);

				await Task.Delay(generationSpeed);
				mazeArray[frontier.Item1, frontier.Item2] = true;
				UpdateCellColour(frontier.Item1, frontier.Item2);
				await Task.Delay(generationSpeed);
				mazeArray[connection.Item1, connection.Item2] = true;
				UpdateCellColour(connection.Item1, connection.Item2);

				GetFrontiers(frontier.Item1, frontier.Item2, mazeArray, frontiers);
			}
			SkipButton.Visibility = Visibility.Hidden;
			CreateStartAndEnd(mazeArray);
			gameInfo.Text = "place cursor on left green cell to start";
		}
		public void InitialiseGrid(bool[,] mazeArray)
		{
			for (int i =  0; i < mazeArray.GetLength(0); i++)
			{
				for (int j = 0; j < mazeArray.GetLength(1); j++)
				{
					mazeArray[i, j] = false;
				}
			}

			MazeGrid.Children.Clear();
			MazeGrid.RowDefinitions.Clear();
			MazeGrid.ColumnDefinitions.Clear();

			for(int i = 0; i < mazeArray.GetLength(0); i++)
			{
				MazeGrid.RowDefinitions.Add(new RowDefinition());
			}
			for (int i = 0; i < mazeArray.GetLength(1); i++)
			{
				MazeGrid.ColumnDefinitions.Add(new ColumnDefinition());
			}

			for (int i = 0; i < mazeArray.GetLength(0); i++)
			{
				for (int j = 0; j < mazeArray.GetLength(1); j++)
				{
					Rectangle cell = new Rectangle()
					{
						Fill = Brushes.Black,
						Stroke = Brushes.Black,
						StrokeThickness = 2
					};
					Grid.SetRow(cell, i);
					Grid.SetColumn(cell, j);
					MazeGrid.Children.Add(cell);
				}
			}
		}

		public void UpdateCellColour(int row, int column)
		{
			foreach (Rectangle rect in MazeGrid.Children)
			{
				if (Grid.GetRow(rect) == row && Grid.GetColumn(rect) == column)
				{
					rect.Fill = Brushes.White;
					rect.Stroke = Brushes.White;
					rect.StrokeThickness = 3;
					break;
				}
			}
		}

		public void GetFrontiers(int row, int column, bool[,] mazeArray, List<(int, int)> frontiers)
		{
			// Check left
			if (column - 2 >= 0 && mazeArray[row,column-2] == false && frontiers.Contains((row,column-2)) == false)
			{
				frontiers.Add((row,column-2));
			}

			// Check right
			if (column + 2 < mazeArray.GetLength(1) && mazeArray[row, column + 2] == false && frontiers.Contains((row, column + 2)) == false)
			{
				frontiers.Add((row, column + 2));
			}

			// Check above
			if (row - 2 >= 0 && mazeArray[row - 2, column] == false && frontiers.Contains((row - 2, column)) == false)
			{
				frontiers.Add((row - 2, column));
			}

			// Check below
			if (row + 2 < mazeArray.GetLength(0) && mazeArray[row + 2, column] == false && frontiers.Contains((row + 2, column)) == false)
			{
				frontiers.Add((row + 2, column));
			}
		}

		public (int, int) FindPassage(int row, int column, bool[,] mazeArray)
		{
			List<(int, int)> possiblePassage = new List<(int, int)>();
			Random rand = new Random();
			
			// Check left
			if (column - 2 >= 0 && mazeArray[row, column - 2] == true)
			{
				possiblePassage.Add((row, column - 2));
			}

			// Check right
			if (column + 2 < mazeArray.GetLength(1) && mazeArray[row, column + 2] == true)
			{
				possiblePassage.Add((row, column + 2));
			}

			// Check above
			if (row - 2 >= 0 && mazeArray[row - 2, column] == true)
			{
				possiblePassage.Add((row - 2, column));
			}

			// Check below
			if (row + 2 < mazeArray.GetLength(0) && mazeArray[row + 2, column] == true)
			{
				possiblePassage.Add((row + 2, column));
			}

			return possiblePassage[rand.Next(0, possiblePassage.Count)];
		}

		public void CreateStartAndEnd(bool[,] mazeArray)
		{
			List<(int, int)> possibleStarts = new List<(int, int)>();
			Random rand = new Random();
			for (int i = 0; i < mazeArray.GetLength(0); i++)
			{
				if (mazeArray[i, 1] == true)
				{
					possibleStarts.Add((i, 0));
				}
			}

			List<(int, int)> possibleEnds = new List<(int, int)>();
			for (int i = 0; i < mazeArray.GetLength(0); i++)
			{
				if (mazeArray[i, mazeArray.GetLength(1) - 2] == true)
				{
					possibleEnds.Add((i, mazeArray.GetLength(1) - 1));
				}
			}

			(int, int) start = possibleStarts[rand.Next(0, possibleStarts.Count)];
			(int, int) end = possibleEnds[rand.Next(0, possibleEnds.Count)];


			foreach (Rectangle rect in MazeGrid.Children)
			{
				if (Grid.GetRow(rect) == start.Item1 && Grid.GetColumn(rect) == start.Item2)
				{
					rect.Fill = Brushes.Green;
					rect.Stroke = Brushes.Green;
					rect.MouseEnter += StartGame;
					break;
				}
			}
			foreach (Rectangle rect in MazeGrid.Children)
			{
				if (Grid.GetRow(rect) == end.Item1 && Grid.GetColumn(rect) == end.Item2)
				{
					rect.Fill = Brushes.Green;
					rect.Stroke = Brushes.Green;
					rect.MouseEnter += Win;
					break;
				}
			}
		}

		public void StartGame(object sender, MouseEventArgs e)
		{
			gameInfo.Text = "STAY ON THE PATH...";
			enableWin = true;
			foreach (Rectangle rect in MazeGrid.Children)
			{
				if (rect.Fill == Brushes.Black)
				{
					rect.MouseEnter += Hover;
				}
			}

			dispatcherTimer.Start();

			string soundPath = System.IO.Path.Combine(projectPath, "sounds", "tick.wav");
			tickPlayer = new SoundPlayer(soundPath);
			tickPlayer.Play();
		}

		public void SkipButton_Click(object sender, RoutedEventArgs e)
		{
			generationSpeed = 0;
			SkipButton.Visibility = Visibility.Hidden;
		}

		public void dispatcherTimer_Tick(object sender, EventArgs e)
		{
			countdownTime--;
			timer.Text = $"remaining: {countdownTime.ToString()}s";
			if (countdownTime == 12)
			{
				string soundPath = System.IO.Path.Combine(projectPath, "sounds", "buildUp.wav");
				buildupPlayer = new SoundPlayer(soundPath);
				buildupPlayer.Play();
			}
			if (countdownTime == 5)
			{
				timer.Foreground = Brushes.Red;
			}
			else if (countdownTime == 0)
			{
				EndText.Text = "you ran out of time.";
				Jumpscare();
			}
		}

		public void Win(object sender, MouseEventArgs e)
		{
			if (enableWin)
			{
				EndText.Text = "congratulations.";
				ShowEnding();
			}
		}
		public async void Hover(object sender, MouseEventArgs e)
		{
			EndText.Text = "you went off the path.";
			Jumpscare();
		}

		public async void Jumpscare()
		{
			JumpscareImage.Visibility = Visibility.Visible;
			PlayGrid.Visibility = Visibility.Hidden;
			dispatcherTimer.Stop();

			string soundPath = System.IO.Path.Combine(projectPath, "sounds", "shriek.wav");
			System.Media.SoundPlayer player = new System.Media.SoundPlayer(soundPath);
			if (jumpSound)
			{
				player.Play();

			}
			await Task.Delay(2000);
			ShowEnding();
		}

		public async void ShowEnding()
		{
			JumpscareImage.Visibility = Visibility.Hidden;
			PlayGrid.Visibility = Visibility.Hidden;
			EndGrid.Visibility = Visibility.Visible;

			dispatcherTimer.Stop();
			if (buildupPlayer != null)
			{
				buildupPlayer.Stop();
			}
			if (tickPlayer != null)
			{
				tickPlayer.Stop();
			}

			EndText.Visibility = Visibility.Hidden;
			AgainButton.Visibility = Visibility.Hidden;

			await Task.Delay(1000);
			EndText.Visibility = Visibility.Visible;
			await Task.Delay(2000);
			AgainButton.Visibility = Visibility.Visible;
		}

		private void Play_Click(object sender, RoutedEventArgs e)
		{
			MainMenu.Visibility = Visibility.Hidden;
			SelectSizeGrid.Visibility = Visibility.Visible;
		}

		private void Small_Click(object sender, RoutedEventArgs e)
		{
			size = "small";
			countdownTime = 15;
			ShowInstructionGrid();
		}
		private void Medium_Click(object sender, RoutedEventArgs e)
		{
			size = "medium";
			countdownTime = 25;
			ShowInstructionGrid();
		}
		private void Large_Click(object sender, RoutedEventArgs e)
		{
			size = "large";
			countdownTime = 35;
			ShowInstructionGrid();
		}

		private void Massive_Click(object sender, RoutedEventArgs e)
		{
			size = "xl";
			countdownTime = 45;
			ShowInstructionGrid();
		}

		private void ShowInstructionGrid()
		{

			SelectSizeGrid.Visibility = Visibility.Hidden;
			InstructionGrid.Visibility = Visibility.Visible;
		}

		private void GenerateMaze_Click(object sender, RoutedEventArgs e)
		{
			InstructionGrid.Visibility = Visibility.Hidden;
			PlayGrid.Visibility = Visibility.Visible;
			timer.Text = $"remaining: {countdownTime.ToString()}s";
			SkipButton.Visibility = Visibility.Visible;
			GenerateMaze(size);
		}
		private void Restart_Click(object sender, RoutedEventArgs e)
		{
			Restart();
		}
	}
}