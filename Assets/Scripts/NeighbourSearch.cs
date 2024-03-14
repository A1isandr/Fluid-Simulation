using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;


public class NeighbourSearch
{
    private Entry[] _spatialLookup;
    private int[] _startIndices;
    private Vector3[] _points;
    private float _radius;
    private readonly (int, int, int)[] _cellOffsets =
    {
        (-1, -1, -1),
        (-1, -1, 0),
        (-1, -1, 1),
        (-1, 0, -1),
        (-1, 0, 0),
        (-1, 0, 1),
        (-1, 1, -1),
        (-1, 1, 0),
        (-1, 1, 1),
        (0, -1, -1),
        (0, -1, 0),
        (0, -1, 1),
        (0, 0, -1),
        (0, 0, 0),
        (0, 0, 1),
        (0, 1, -1),
        (0, 1, 0),
        (0, 1, 1),
        (1, -1, -1),
        (1, -1, 0),
        (1, -1, 1),
        (1, 0, -1),
        (1, 0, 0),
        (1, 0, 1),
        (1, 1, -1),
        (1, 1, 0),
        (1, 1, 1)
    };

    public NeighbourSearch(int numParticles)
    {
        _spatialLookup = new Entry[numParticles];
        _startIndices = new int[numParticles];
    }

    /// <summary>
    /// Defines entry in a spatial lookup.
    /// </summary>
    /// <param name="ParticleIndex"></param>
    /// <param name="CellKey"></param>
    private record Entry(int ParticleIndex, uint CellKey) : IComparable
    {
        public int CompareTo(object incomingObject)
        {
            var incomingEntry = incomingObject as Entry;

            return CellKey.CompareTo(incomingEntry?.CellKey);
        }
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="samplePoint"></param>
    public IEnumerable<int> ForeachPointWithinRadius(Vector3 samplePoint)
    {
        // Find which cell the sample point is in (this will be the centre of 3x3x3 block).
        (int centreX, int centreY, int centreZ) = PositionToCellCoord(samplePoint, _radius);
        float sqrRadius = _radius * _radius;
        
        // Loop over all cells of the 3x3x3 block around the centre cell.
        foreach ((int offsetX, int offsetY, int offsetZ) in _cellOffsets)
        {
            // Get key of current cell, then loop over all points that share that key.
            uint key = GetKeyFromHash(HashCell(centreX + offsetX, centreY + offsetY, centreZ + offsetZ));
            int cellStartIndex = _startIndices[key];

            for (int i = cellStartIndex; i < _spatialLookup.Length; i++)
            {
                // Exit loop if we`re no longer looking at the correct cell.
                if (_spatialLookup[i].CellKey != key) yield break;

                int particleIndex = _spatialLookup[i].ParticleIndex;
                float sqrDst = (_points[particleIndex] - samplePoint).sqrMagnitude;
                
                // Test if the point is inside the radius.
                if (sqrDst <= sqrRadius)
                {
                    yield return particleIndex;
                }
            }
        }
    }
    
    /// <summary>
    /// Updates spatial lookup.
    /// </summary>
    /// <param name="points"></param>
    /// <param name="radius"></param>
    public void UpdateSpatialLookup(Vector3[] points, float radius)
    {
        _points = points;
        _radius = radius;
        
        // Create (unordered) spatial lookup.
        Parallel.For(0, points.Length, i =>
        {
            (int cellX, int cellY, int cellZ) = PositionToCellCoord(points[i], radius);
            uint cellKey = GetKeyFromHash(HashCell(cellX, cellY, cellZ));
            _spatialLookup[i] = new Entry(i, cellKey);
            _startIndices[i] = int.MaxValue; // Reset start index.
        });
		
        // Sort by cell key.
        Array.Sort(_spatialLookup);
        
        // Calculate start indices of each unique cell key in the spatial lookup.
        Parallel.For(0, points.Length, i =>
        {
            uint key = _spatialLookup[i].CellKey;
            uint keyPrev = i == 0 ? uint.MaxValue : _spatialLookup[i - 1].CellKey;

            if (key != keyPrev)
            {
                _startIndices[key] = i;
            }
        });
    }
    
    /// <summary>
    /// Converts point position to cell coord.
    /// </summary>
    /// <param name="point"></param>
    /// <param name="radius"></param>
    /// <returns>Cell coord.</returns>
    private (int x, int y, int z) PositionToCellCoord(Vector3 point, float radius)
    {
        int cellX = (int)(point.x / radius);
        int cellY = (int)(point.y / radius);
        int cellZ = (int)(point.z / radius);

        return (cellX, cellY, cellZ);
    }
    
    /// <summary>
    /// Generates hash based on cell coord.
    /// </summary>
    /// <param name="cellX"></param>
    /// <param name="cellY"></param>
    /// <param name="cellZ"></param>
    /// <returns>Cell's hash.</returns>
    private uint HashCell(int cellX, int cellY, int cellZ)
    {
        uint a = (uint)cellX * 15823;
        uint b = (uint)cellY * 9737333;
        uint c = (uint)cellZ * 15490897;

        return a + b + c;
    }
    
    /// <summary>
    /// Gets key from cell's hash.
    /// </summary>
    /// <param name="hash"></param>
    /// <returns>Cell's key.</returns>
    private uint GetKeyFromHash(uint hash)
    {
        return hash % (uint)_spatialLookup.Length;
    }
}