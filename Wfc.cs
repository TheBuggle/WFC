using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

public partial class Wfc : Node
{
    [Export] Control parent;
    [Export] Vector2I gridShape;
    [Export] Vector2I kernelShape;
    [Export] Texture2D sampleTexture;

    MarginContainer[,] cellContainers;
    Dictionary<Color, int> coloursToIndices;
    List<Color> indicesToColours;
    double[] priors;
    Dictionary<Kernel, double>[] conditionals;
    double[,] probabilities;
    PriorityQueue<Vector2I, double> tileQueue;
    bool[] collapsedTiles;

    public override void _EnterTree()
    {
        base._EnterTree();

        CalculateProbabilities();
        InitialiseTiles();

        tileQueue = new PriorityQueue<Vector2I, double>();
        Random random = new Random();
        int randX = random.Next(0, gridShape.X), randY = random.Next(0, gridShape.Y);
        tileQueue.Enqueue(new Vector2I(randX, randY), 0);

        InitialiseVisuals();
    }

    void CalculateProbabilities()
    {
        Image sampleImage = sampleTexture.GetImage();
        FindUniqueColors(sampleImage);
        CalculatePriorsAndConditionals(sampleImage);
    }

    void InitialiseTiles()
    {
        probabilities = new double[gridShape.X * gridShape.Y, coloursToIndices.Count];
        collapsedTiles = new bool[gridShape.X * gridShape.Y];
        for (int x = 0; x < gridShape.X; x++)
        {
            for (int y = 0; y < gridShape.Y; y++)
            {
                for (int i = 0; i < coloursToIndices.Count; i++)
                {
                    probabilities[x * gridShape.X + y, i] = priors[i];
                }
                collapsedTiles[x * gridShape.X + y] = false;
            }
        }
    }

    Color GetColorFromProbability(int x, int y)
    {
        Color cellColor = new Color(0, 0, 0, 1);
        for (int i = 0; i < coloursToIndices.Count; i++)
        {
            cellColor.R += indicesToColours[i].R * (float)probabilities[x * gridShape.X + y, i];
            cellColor.G += indicesToColours[i].G * (float)probabilities[x * gridShape.X + y, i];
            cellColor.B += indicesToColours[i].B * (float)probabilities[x * gridShape.X + y, i];
        }
        return cellColor;
    }

    void UpdateTileColor(int x, int y)
    {
        cellContainers[x, y].GetChild<ColorRect>(0).Color = GetColorFromProbability(x, y);
    }

    void CalculatePriorsAndConditionals(Image sampleImage)
    {
        priors = new double[coloursToIndices.Count];
        conditionals = new Dictionary<Kernel, double>[coloursToIndices.Count];
        for (int i = 0; i < coloursToIndices.Count; i++)
        {
            conditionals[i] = new Dictionary<Kernel, double>();
        }

        int color, index;
        double count;
        Vector2I kernelCoordinate;
        int[] indexedKernel = new int[kernelShape.X * kernelShape.Y - 1];
        Kernel kernel;
        for (int x = 0; x < sampleImage.GetWidth(); x++)
        {
            for (int y = 0; y < sampleImage.GetHeight(); y++)
            {
                // priors
                color = coloursToIndices[sampleImage.GetPixel(x, y)];
                priors[color]++;

                // conditionals
                for (int j = 0; j < kernelShape.X; j++)
                {
                    for (int k = 0; k < kernelShape.Y; k++)
                    {
                        index = j * kernelShape.X + k;
                        if (index == (kernelShape.X * kernelShape.Y - 1) / 2) { continue; }
                        kernelCoordinate = new Vector2I(
                            Wrap(x + j - (kernelShape.X - 1) / 2, 0, sampleImage.GetWidth()),
                            Wrap(y + k - (kernelShape.Y - 1) / 2, 0, sampleImage.GetHeight())
                        );
                        if (index > (kernelShape.X - kernelShape.Y - 1) / 2) { index--; }
                        indexedKernel[index] = coloursToIndices[sampleImage.GetPixelv(kernelCoordinate)];
                    }
                }
                kernel = new Kernel(indexedKernel);
                if (conditionals[color].TryGetValue(kernel, out count))
                {
                    conditionals[color][kernel] = count + 1;
                }
                else
                {
                    conditionals[color][kernel] = 1;
                }
            }
        }


        for (int i = 0; i < coloursToIndices.Count; i++)
        {
            foreach (Kernel key in conditionals[i].Keys.ToList<Kernel>())
            {
                conditionals[i][key] = conditionals[i][key] / priors[i];
            }
            priors[i] = priors[i] / (sampleImage.GetWidth() * sampleImage.GetHeight());
        }
    }

    int Wrap(int value, int inclusiveLowerBound, int exclusiveUpperBound)
    {
        if (value < inclusiveLowerBound)
        {
            return exclusiveUpperBound - inclusiveLowerBound + value;
        }
        else if (value >= exclusiveUpperBound)
        {
            return inclusiveLowerBound + value - exclusiveUpperBound;
        }
        else
        {
            return value;
        }
    }

    void FindUniqueColors(Image sampleImage)
    {
        coloursToIndices = new Dictionary<Color, int>();
        indicesToColours = new List<Color>();

        for (int x = 0; x < sampleImage.GetWidth(); x++)
        {
            for (int y = 0; y < sampleImage.GetHeight(); y++)
            {
                if (coloursToIndices.ContainsKey(sampleImage.GetPixel(x, y))) { continue; }
                coloursToIndices.Add(sampleImage.GetPixel(x, y), indicesToColours.Count);
                indicesToColours.Add(sampleImage.GetPixel(x, y));
            }
        }
    }

    void InitialiseVisuals()
    {
        cellContainers = new MarginContainer[gridShape.X, gridShape.Y];
        for (int x = 0; x < gridShape.X; x++)
        {
            VBoxContainer columnContainer = new VBoxContainer();
            columnContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            parent.AddChild(columnContainer);

            for (int y = 0; y < gridShape.Y; y++)
            {
                MarginContainer cellContainer = new MarginContainer();
                cellContainers[x, y] = cellContainer;
                cellContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
                columnContainer.AddChild(cellContainer);

                ColorRect cellColorBlock = new ColorRect();
                cellColorBlock.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                cellColorBlock.Color = GetColorFromProbability(x, y);
                cellContainer.AddChild(cellColorBlock);
            }
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.IsPressed() && keyEvent.Keycode == Key.Space)
        {
            DoStep();
        }
    }

    void DoStep()
    {
        Stack<Vector2I> propagationStack = new Stack<Vector2I>();
        CollapsedTile(propagationStack);

    }

    void CollapsedTile(Stack<Vector2I> propagationStack)
    {
        Vector2I nextTile = tileQueue.Dequeue();
        int collapsedColorIndex = IndexOfMaxProbability(nextTile.X, nextTile.Y);

        probabilities[nextTile.X * gridShape.X + nextTile.Y, collapsedColorIndex] = 1.0;
        collapsedTiles[nextTile.X * gridShape.X + nextTile.Y] = true;
        UpdateTileColor(nextTile.X, nextTile.Y);

        tileQueue.Clear();
        for (int x = 0; x < kernelShape.X / 2; x++)
        {
            for (int y = 0; y < kernelShape.Y / 2; y++)
            {
                Vector2I neighbourCoordinate = new Vector2I(
                    Wrap(nextTile.X + x, 0, gridShape.X),
                    Wrap(nextTile.Y + y, 0, gridShape.Y)
                );
                if (collapsedTiles[neighbourCoordinate.X * gridShape.X + neighbourCoordinate.Y]) { continue; }
                tileQueue.Enqueue(neighbourCoordinate, IndexOfMaxProbability(neighbourCoordinate.X,
                    neighbourCoordinate.Y)); 
            }
        }
    }

    int IndexOfMaxProbability(int x, int y)
    {
        int maxIndex = 0;
        double maxProbability = probabilities[x * gridShape.X + y, 0];
        for (int i = 1; i < coloursToIndices.Count; i++)
        {
            if (probabilities[x * gridShape.X + y, i] > maxProbability)
            {
                maxProbability = probabilities[x * gridShape.X + y, i];
                maxIndex = i;
            }
        }
        return maxIndex;
    }
}

