﻿using MathSupport;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
namespace _051colormap
{
  #region general helpers
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
  #endregion

  #region colors sorting
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
  #endregion

  #region K-means++ clustering
  /// <summary>
  /// k-means++ clusters centroids initializator.
  /// </summary>
  public class CentroidInitializator {
      Bitmap _input;
      int _centroidsCount;
    
      List<(int x, int y)> _centroids;

     public CentroidInitializator (Bitmap inputData, int centroidsCount) {
      _input = inputData;
      _centroidsCount = centroidsCount;
      _centroids = new List<(int x, int y)>(_centroidsCount);

      for (int i = 0; i < _centroidsCount; i++) {
        _centroids.Add((-1,-1));
      }
    }

    public void InitializeFirstCentroid () {

      var rnd = new Random();
      /*int colPiv = rnd.Next(_input.Width-1);
      int rowPiv = rnd.Next(_input.Height-1);*/
      int randomRadius = rnd.Next(Math.Min(_input.Width / 2, _input.Height / 2));
      _centroids[0] = (_input.Width / 2 - randomRadius, _input.Height / 2 - randomRadius);
      
    }

    public void InitializeRemainingCentroids () {

      Dictionary<(int x, int y), int> minimalDistances = new Dictionary<(int x, int y), int>(Math.Abs(_input.Width * _input.Height - _centroidsCount));

      for (int i = 0; i < _centroidsCount - 1; i++)
      {
        Color currentCentroidCol = _input.GetPixel(_centroids[i].x, _centroids[i].y);

        for (int y = 0; y < _input.Height; y++) {
          for (int x = 0; x < _input.Width; x++) {
            
            if (!_centroids.Contains((x, y))) { // if not a centroid yet

              int dist = Helpers.GetColorDistancePow2(_input.GetPixel(x, y), currentCentroidCol); // select max

              if (i == 0) {
                minimalDistances.Add((x, y), dist);
              }
              else {
                if (dist < minimalDistances[(x, y)]) {
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

    public List<(int x, int y)> GenerateCentroids () {
      InitializeFirstCentroid();
      InitializeRemainingCentroids();
      return _centroids;
    }

  }

  /// <summary>
  /// Finds K centroids of clusters generated by K means clustering algorithm.
  /// </summary>
  public class KMeansCentroidsFinder {

    private class ClusterComputationData {
      /// <summary> Sum of all colors Red channel values in cluster. </summary>
      public uint R { get; set; }
      /// <summary> Sum of all colors Blue channel values in cluster. </summary>
      public uint G { get; set; }
      /// <summary> Sum of all colors Green channel values in cluster. </summary>
      public uint B { get; set; }
      /// <summary> Number of colors counted in.</summary>
      public uint Ctr { get; set; }

      public void Deconstruct (out uint R, out uint G, out uint B, out uint Ctr)
      { R = this.R; G = this.G; B = this.B; Ctr = this.Ctr; }
    }

    Bitmap _input;
    int _clustersCount;

    Dictionary<(byte R, byte G, byte B), ClusterComputationData> _currentCentroids =
      new Dictionary<(byte R, byte G, byte B), ClusterComputationData>();

    bool _isClustering = true; // invariant until the end of algorithm
    int _remainingCycles; // number of remaining clustering cycles
    int _clusterDistThreshold;

    /// <param name="inputData"></param>
    /// <param name="clustersCount">Number of clusters</param>
    /// <param name="clusteringCycles">Maximal number of clustering cycles.</param>
    /// <param name="clusterDistThreshold">The minimal distance of color from particular centroid to be counted in it's cluster.</param>
    public KMeansCentroidsFinder (Bitmap inputData, int clustersCount, int clusteringCycles = int.MaxValue, int clusterDistThreshold = int.MaxValue) {
      _input = inputData;
      _clustersCount = clustersCount;
      _remainingCycles = clusteringCycles;
      _clusterDistThreshold = clusterDistThreshold;
    }

    private (byte R, byte G, byte B) FindClosestCentroid (ref Color col) {

      (byte R, byte G, byte B) closestCentroid = _currentCentroids.Keys.First(); // todo: i am not sure what this returns 1? or it blows up XD
      double minDist = double.MaxValue;

      foreach (var centroid in _currentCentroids) {
        double dist = Math.Sqrt(Helpers.GetColorDistancePow2(col, centroid.Key));

        if (dist < minDist) {
          closestCentroid = centroid.Key;
          minDist = dist;
        }
      }

      return closestCentroid;
    }

    
    public List<Color> GetClusterCentroids () {
      var result = new List<Color>();
      foreach (var centroid in _currentCentroids) {
        (byte R, byte G, byte B) = centroid.Key;
        result.Add(Color.FromArgb(R, G, B));
      }

      return result;
    }
    
    private void UpdateClusterCycle () {

      var updatedClusters = new Dictionary<(byte R, byte G, byte B), ClusterComputationData>(_currentCentroids.Count);

      int wellPositionedCount = 0;
      foreach (var cluster in _currentCentroids) {
        (uint R, uint G, uint B, uint ctr) = cluster.Value;

        var newCentroid = ((byte)(R / ctr), (byte)(G / ctr), (byte)(B / ctr));

        updatedClusters.Add(newCentroid, new ClusterComputationData());

        if (newCentroid == cluster.Key) {
          wellPositionedCount++; // if mean is same as current centroid value => this cluster is done.
        }
          
      }

      if (_currentCentroids.Count == wellPositionedCount || _remainingCycles == 0)
        _isClustering = false; // all clusters done or we hit the cycles limit => end

      _currentCentroids = updatedClusters;
      _remainingCycles--;
    }

    public void CalculateClusteringCentroids () {

      while (_isClustering) {
        for (int y = 0; y < _input.Height; y++) {
          for (int x = 0; x < _input.Width; x++) {
            Color col = _input.GetPixel(x, y);

            var closestClusterCentroid = FindClosestCentroid(ref col);
            var centroids = _currentCentroids[closestClusterCentroid];

            if (Helpers.GetColorDistancePow2(col, closestClusterCentroid) <= _clusterDistThreshold) {
              centroids.R += col.R;
              centroids.G += col.G;
              centroids.B += col.B;
              centroids.Ctr++;
            }
          }
        }

        UpdateClusterCycle();
      }
    }

    /// <summary> Initializes kmeans clustering algorithm. </summary>
    public void InitializeCentroids() {
      var centI = new CentroidInitializator(_input, _clustersCount);
      _currentCentroids = new Dictionary<(byte R, byte G, byte B), ClusterComputationData>(_clustersCount);

      foreach (var centroidCord in centI.GenerateCentroids()) {
        var (x, y) = centroidCord;
        var col = _input.GetPixel(x, y);
        if (!_currentCentroids.ContainsKey((col.R, col.G, col.B)))
          _currentCentroids.Add((col.R, col.G, col.B), new ClusterComputationData());
      }
    }

  }

  #endregion

  #region Other util classes
  /// <summary> Finds closest colors in real image to the reference ones. </summary>
  class RealColorsFinder {

    Bitmap _input;
    List<Color> _colorsToFind;
    List<Color> _colorsInImage;
    public RealColorsFinder(Bitmap input, List<Color> colorsToFind) {
      _input = input;
      _colorsToFind = colorsToFind;
      _colorsInImage = new List<Color>(_colorsToFind.Count);
    }

    private Color FindClosestRealColor(Color originalColor) {
      
      int distance = int.MaxValue;
      Color closestColor = originalColor;
      for (int y = 0; y < _input.Height; y++) {
        for (int x = 0; x < _input.Width; x++) {
          int newDistance;
          Color currentRealCol = _input.GetPixel(x, y);
          if ((newDistance = Helpers.GetColorDistancePow2(currentRealCol, originalColor)) < distance) {
            distance = newDistance;
            closestColor = currentRealCol;
          }
        }
      }
      return closestColor;
    }

    private void FindRealColors () {
      foreach (var originalCol in _colorsToFind) {
        _colorsInImage.Add(FindClosestRealColor(originalCol));
      } 
    }

    public List<Color> GetRealColors () {

      if (_colorsInImage.Count == 0)
        FindRealColors();

      return _colorsInImage;
    }

  }

  /// <summary> Randomly picks colors from image. </summary>
  public class RandomColorPicker {
    int _colorsCount;
    Bitmap _input;
    List<Color> _colors = new List<Color>();

    public RandomColorPicker (Bitmap input, int colorsCount) {
      _input = input;
      _colorsCount = colorsCount;
    }

    private void PickRandomColors () {
      var rnd = new Random();

      for (int i = 0; i < _colorsCount; i++) {
        int x = rnd.Next(_input.Width-1);
        int y = rnd.Next(_input.Height-1);
        _colors.Add(_input.GetPixel(x, y));
      }
    }

    public List<Color> GetRandomColors () {

      if (_colors.Count != _colorsCount)
        PickRandomColors();

      return _colors;
    }

  }

  #endregion

  class Colormap
  {
    /// <summary>
    /// Form data initialization.
    /// </summary>
    public static void InitForm (out string author)
    {
      author = "Ondřej Kříž";
    }

    private static List<Color> FindInitialClusteredColors (Bitmap input, int numCol)
    {
      var kmeansClustering = new KMeansCentroidsFinder(input, numCol, 3, 5);

      kmeansClustering.InitializeCentroids();
      kmeansClustering.CalculateClusteringCentroids();
      return kmeansClustering.GetClusterCentroids();
    }

    private static List<Color> FindRealColors (Bitmap input, List<Color> referenceColors) {
      var realColorsFinder = new RealColorsFinder(input, referenceColors);
      return realColorsFinder.GetRealColors();
    }

    private static void FilterColorWithLowestValueInHSV(List<Color> colors) {

      double lowestValue = double.MaxValue;
      int lowestColIndex = 0;
      for(int i = 0; i < colors.Count; i++) {
        double H, S, V;
        Arith.ColorToHSV(colors[i], out H, out S, out V);
        if(V < lowestValue) {
          lowestValue = V;
          lowestColIndex = i;
        }
      }

      colors.RemoveAt(lowestColIndex);
    }

    private static List<Color> CompleteColorsSearch(Bitmap input, List<Color> colorsFound, int numCol)
    {
      List<Color> result = new List<Color>(numCol);

      result.AddRange(colorsFound);

      // if image didn't have enough distinct colors...
      if (colorsFound.Count < numCol) { // we randomly pick
        var randColPicker = new RandomColorPicker(input, numCol - colorsFound.Count);
        var randomColors = randColPicker.GetRandomColors();
        result.AddRange(randomColors);
      }

      return result;
    }

    /// <summary>
    /// Generate a colormap based on input image.
    /// </summary>
    /// <param name="input">Input raster image.</param>
    /// <param name="numCol">Required colormap size (ignore it if you must).</param>
    /// <param name="colors">Output palette (array of colors).</param>
    public static void Generate (Bitmap input, int numCol, out Color[] colors) {

      colors = new Color[numCol];

      int fakeColsCount = numCol < 5 ? numCol + 1 : numCol;

      // cluster image data, and get clusters centroids
      var clusteredColors = FindInitialClusteredColors(input, fakeColsCount);

      // find real colors in image
      var realColors = FindRealColors(input, clusteredColors);

      // complete colors
      var colorsListCompleted = CompleteColorsSearch(input, realColors, fakeColsCount);

      // e. g. filter darkest col if not enough colors
      if (numCol < 5)
        FilterColorWithLowestValueInHSV(colorsListCompleted);

      // sort colors
      var sorter = new SortByHue(colorsListCompleted.ToArray());
      colors = sorter.SortColors();

    }
  }
}
