  using System.Drawing;
using MathSupport;
using System;
using System.Collections.Generic;

namespace _051colormap
{

  public static class Helpers {


    public static int GetColorDistancePow2 (Color col1, Color col2)
    {

      int res = (int)Math.Pow(col1.R - col2.R, 2) + (int)Math.Pow(col1.G - col2.G, 2) + (int)Math.Pow(col1.B - col2.B, 2);
      return res;
    }
    public static int GetColorDistancePow2 (Color col1, (byte R, byte G, byte B) col2) {
      int res = (int)Math.Pow(col1.R - col2.R, 2) + (int)Math.Pow(col1.G - col2.G, 2) + (int)Math.Pow(col1.B - col2.B, 2);
      return res;
    }

  }

  public interface IColorSort {
    Color[] SortColors ();
  }

  public class SortByHue : IColorSort {

    private List<(double hue, Color color)> _colors;
    private Color[] _result;
    public SortByHue(Color[] colors) {
      _colors = new List<(double, Color)>(colors.Length);
      _result = new Color[colors.Length];
      InitializeColors(colors);
    }

    private void InitializeColors (Color[] colors) {

      foreach (var col in colors) {
        double H, S, V;
        Arith.ColorToHSV(col, out H, out S, out V);
        _colors.Add((H, col));
      }
    }

    public Color[] SortColors () {
      SortInternalList();
      InitializeResult();
      return _result;
    }

    private void SortInternalList() => _colors.Sort((col1, col2) => col1.hue.CompareTo(col2.hue));
    private void InitializeResult () {
      for(int i = 0; i < _colors.Count; i++)
        _result[i] = _colors[i].color;
    }
  }


  public class CentroidInitializator
  {
      Bitmap _input;
      int _centroidsCount;
    //Dictionary<(int x, int y), List<(int x, int y)>> _clustersData;
    List<(int x, int y)> _centroids;

     public CentroidInitializator (Bitmap inputData, int centroidsCount) {
      _input = inputData;
      _centroidsCount = centroidsCount;
      _centroids = new List<(int x, int y)>(_centroidsCount);

      for (int i = 0; i < _centroidsCount; i++) {
        _centroids.Add((-1,-1));
      }

      // todo: případně i omezit dopředu velikost clusteru
    }

    public void InitializeFirstCentroid () {

      var rnd = new Random();
      int colPiv = rnd.Next(_input.Width-1);
      int rowPiv = rnd.Next(_input.Height-1);
      _centroids[0] = (colPiv, rowPiv);

    }

    public void InitializeRemainingCentroids () {
      
      Dictionary<(int x, int y), int> minimalDistances = new Dictionary<(int x, int y), int>(_input.Width*_input.Height - _centroidsCount);

      //((int x, int y), int val) closestPoint; // candidate for next centroid


      for (int i = 0; i < _centroidsCount - 1; i++) // for each centroid
      {
        Color currentCentroidCol = _input.GetPixel(_centroids[i].x, _centroids[i].y);

        for (int y = 0; y < _input.Height; y++) {
          for (int x = 0; x < _input.Width; x++) {
            
            if (!_centroids.Contains((x, y))) { // not a centroid

              int dist = Helpers.GetColorDistancePow2(_input.GetPixel(x, y), currentCentroidCol); // select max


              if (i == 0) {
                minimalDistances.Add((x, y), dist);
              }
              else {
             //  Console.WriteLine(minimalDistances[(x, y)]);
                if (dist < minimalDistances[(x, y)]) {
                  /*Console.WriteLine(dist);
                  Console.WriteLine(minimalDistances[(x, y)]);*/
                  minimalDistances[(x, y)] = dist;
                }
              }

            }
          }
        }

        ((int x, int y), int value) maxFromMins = ((0, 0), 0);

        foreach (var item in minimalDistances)
        {
          if (item.Value > maxFromMins.value && !_centroids.Contains(item.Key)) {
            maxFromMins = (item.Key, item.Value);
          }
        }
        _centroids[i + 1] = maxFromMins.Item1;
      }
    }

    public List<(int x, int y)> InitializeCluster () {
      InitializeFirstCentroid();
      InitializeRemainingCentroids();
      return _centroids;
    }

  }


  public class kMeansClustering {

    Bitmap _input;
    int _clustersCount;

    Dictionary<(int x, int y), (byte R, byte G, byte B)> _previousClusterRes;

    Dictionary<(int x, int y), (int R, int G, int B)> _currentClustersRes;

    Dictionary<(int x, int y), int> _currentClusterColCount = new Dictionary<(int x, int y), int>();

    //List<(byte R, byte G, byte B)> _previousCentroids = new List<(byte R, byte G, byte B)>();
    Dictionary<(byte R, byte G, byte B), (int R, int G, int B, int ctr)> _currentCentroids = new Dictionary<(byte R, byte G, byte B), (int R, int G, int B, int ctr)>();


    bool _isClustering = true;

    

    kMeansClustering (Bitmap inputData, int clustersCount) {
      _input = inputData;
      _clustersCount = clustersCount;

    }

    private (byte R, byte G, byte B) FindClosestCentroid (ref Color col) {

      (byte R, byte G, byte B) closestCentroid = _currentCentroids.Keys.GetEnumerator().Current; // todo: i am not sure what this returns 1? or it blows up XD
      int minDist = int.MaxValue;

      foreach (var centroid in _currentCentroids) {
        int dist = Helpers.GetColorDistancePow2(col, centroid.Key);

        if (dist < minDist) {
          closestCentroid = centroid.Key;
          minDist = dist;
        }

      }

      return closestCentroid;
    }

    public void UpdateClusterData () {

      var updatedClusters = new Dictionary<(byte R, byte G, byte B), (int R, int G, int B, int ctr)>(_currentCentroids.Count);

      foreach (var cluster in _currentCentroids) {
        (int R, int G, int B, int ctr) = cluster.Value;

        var newCentroid = ((byte)(R / ctr), (byte)(G / ctr), (byte)(B / ctr));

        
        if (newCentroid != cluster.Key)
          updatedClusters.Add(newCentroid, (0, 0, 0, 0));
      }

      if (updatedClusters.Count == 0) // no more updates => end
        _isClustering = false;

    }

    public void UpdateClustering () {
      while (_isClustering) {
        for (int y = 0; y < _input.Height; y++) {
          for (int x = 0; x < _input.Width; x++) {
            Color col = _input.GetPixel(x, y);
            var closestClusterCentroid = FindClosestCentroid(ref col);
            var centroids = _currentCentroids[closestClusterCentroid];

            centroids.R += col.R;
            centroids.G += col.G;
            centroids.B += col.B;
            centroids.ctr++;
          }
        }
      }
    }

    public void InitializeCentroids() {
      var centI = new CentroidInitializator(_input, _clustersCount);
      foreach(var centroidCord in centI.InitializeCluster()) {
        var (x, y) = centroidCord;
        var col = _input.GetPixel(x, y);
        _previousClusterRes.Add((x, y), (col.R, col.G, col.B));
        _currentClustersRes.Add((x, y), (0, 0, 0));
      }

      /*for (int i = 0; i < _clustersCount; i++) {

      }*/

    }

  }

  class Colormap
  {
    /// <summary>
    /// Form data initialization.
    /// </summary>
    public static void InitForm (out string author)
    {
      author = "Ondřej Kříž";
    }

    /// <summary>
    /// Generate a colormap based on input image.
    /// </summary>
    /// <param name="input">Input raster image.</param>
    /// <param name="numCol">Required colormap size (ignore it if you must).</param>
    /// <param name="colors">Output palette (array of colors).</param>
    public static void Generate (Bitmap input, int numCol, out Color[] colors)
    {
      // !!!{{ TODO - generate custom palette based on the given image
      colors = new Color[numCol];

      var centI = new CentroidInitializator(input, numCol);
      var centroids = centI.InitializeCluster();

      for (int i = 0; i < numCol; i++) {
       // Console.WriteLine(centroids[i]);
        colors[i] = input.GetPixel(centroids[i].x, centroids[i].y);
      }

      /*var sorter = new SortByHue(colors);
      colors = sorter.SortColors();*/


      return;

   /*   int width  = input.Width;
      int height = input.Height;

      colors = new Color[numCol];            // accepting the required palette size..

      colors[0] = input.GetPixel(0, 0);      // upper left image corner

      double H, S, V;
      Color center = input.GetPixel(width / 2, height / 2);   // image center
      Arith.ColorToHSV(center, out H, out S, out V);
      if (S > 1.0e-3)
        colors[numCol - 1] = Arith.HSVToColor(H, 1.0, 1.0);   // non-monochromatic color => using Hue only
      else
        colors[numCol - 1] = center;                          // monochromatic color => using it directly

      // color-ramp linear interpolation:
      float r = colors[0].R;
      float g = colors[0].G;
      float b = colors[0].B;
      float dr = (colors[numCol - 1].R - r) / (numCol - 1.0f);
      float dg = (colors[numCol - 1].G - g) / (numCol - 1.0f);
      float db = (colors[numCol - 1].B - b) / (numCol - 1.0f);

      for (int i = 1; i < numCol; i++)
      {
        r += dr;
        g += dg;
        b += db;
        colors[i] = Color.FromArgb((int)r, (int)g, (int)b);
      }*/

      // !!!}}
    }
  }
}
