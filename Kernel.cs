using System;
using System.Collections;
using System.Collections.Generic;
using Godot;

public class Kernel
{
    public int[] neighbourColorIndices;
    public Kernel(int[] neighbourColorIndices)
    {
        this.neighbourColorIndices = neighbourColorIndices.Clone() as int[];
    }

    public override int GetHashCode()
    {
        return ((IStructuralEquatable)neighbourColorIndices).GetHashCode(EqualityComparer<int>.Default);
    }

    public override bool Equals(object obj)
    {
        if (obj is Kernel other)
        {
            // thank you internet
            return StructuralComparisons.StructuralEqualityComparer.Equals(
                this.neighbourColorIndices, other.neighbourColorIndices);
        }
        return false;
    }

    public override string ToString()
    {
        return "[" + String.Join(", ", neighbourColorIndices) + "]";
    }
}