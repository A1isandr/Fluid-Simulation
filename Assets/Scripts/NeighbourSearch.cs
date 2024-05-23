using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;


public class NeighbourSearch
{
    public readonly Vector3[] cellsCoord;

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
        cellsCoord = new Vector3[numParticles];
        _spatialLookup = new Entry[numParticles];
        _startIndices = new int[numParticles];
    }

    /// <summary>
    /// Вхождение в массив пространственного поиска.
    /// </summary>
    /// <param name="ParticleIndex"></param>
    /// <param name="CellKey"></param>
    public record Entry(int ParticleIndex, uint CellKey) : IComparable
    {
        public int CompareTo(object incomingObject)
        {
            var incomingEntry = incomingObject as Entry;

            return CellKey.CompareTo(incomingEntry?.CellKey);
        }
    }
    
    /// <summary>
    /// Перебирает всех соседей данной точки в пространстве.
    /// </summary>
    /// <param name="samplePoint"></param>
    public IEnumerable<int> ForeachPointWithinRadius(Vector3 samplePoint)
    {
        // Находим в какой ячейке располагается данная точка (центр куба 3x3x3).
        (int centreX, int centreY, int centreZ) = PositionToCellCoord(samplePoint, _radius);
        float sqrRadius = _radius * _radius;
        
        // Проходимся по всем ячейкам куба 3x3x3 вокруг центральной.
        foreach ((int offsetX, int offsetY, int offsetZ) in _cellOffsets)
        {
            // Получаем ключ текущей ячейки, затем проходимся по всем точкам, имеющим такой же ключ.
            uint key = GetKeyFromHash(HashCell(centreX + offsetX, centreY + offsetY, centreZ + offsetZ));
            int cellStartIndex = _startIndices[key];

            for (int i = cellStartIndex; i < _spatialLookup.Length; i++)
            {
                // Выходим из цикла, если точка в не нужной нам ячейке.
                if (_spatialLookup[i].CellKey != key) break;

                int particleIndex = _spatialLookup[i].ParticleIndex;
                float sqrDst = (_points[particleIndex] - samplePoint).sqrMagnitude;
                
                // Лежит ли точка в пределах радиуса.
                if (sqrDst <= sqrRadius)
                {
                    yield return particleIndex;
                }
            }
        }
    }
    
    /// <summary>
    /// Обновляет пространственный поиск.
    /// </summary>
    /// <param name="points"></param>
    /// <param name="radius"></param>
    public void UpdateSpatialLookup(Vector3[] points, float radius)
    {
        _points = points;
        _radius = radius;
        
        // Создаем (несортированный) пространственный поиск.
        Parallel.For(0, points.Length, i =>
        {
            (int cellX, int cellY, int cellZ) = PositionToCellCoord(points[i], radius);
            cellsCoord[i] = new Vector3(cellX, cellY, cellZ);
            uint cellKey = GetKeyFromHash(HashCell(cellX, cellY, cellZ));
            _spatialLookup[i] = new Entry(i, cellKey);
            _startIndices[i] = int.MaxValue; // Сбрасываем стартовый индекс.
        });
		
        // Сортируем по ключу ячейки.
        Array.Sort(_spatialLookup);
        
        // Рассчитываем начальный индекс для каждой уникальной ячейки в пространственном поиске.
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
    /// Конвертирует положение точки в координаты ячейки, в которой она раполагается.
    /// </summary>
    /// <param name="point"></param>
    /// <param name="radius"></param>
    /// <returns>Координаты ячейки.</returns>
    private (int x, int y, int z) PositionToCellCoord(Vector3 point, float radius)
    {
        int cellX = (int)(point.x / radius);
        int cellY = (int)(point.y / radius);
        int cellZ = (int)(point.z / radius);

        return (cellX, cellY, cellZ);
    }
    
    /// <summary>
    /// Генерирует хэш, основываясь на координатах ячейки.
    /// </summary>
    /// <param name="cellX"></param>
    /// <param name="cellY"></param>
    /// <param name="cellZ"></param>
    /// <returns>Хэш ячейки.</returns>
    private uint HashCell(int cellX, int cellY, int cellZ)
    {
        uint a = (uint)cellX * 15823;
        uint b = (uint)cellY * 9737333;
        uint c = (uint)cellZ * 440817757;

        return a + b + c;
    }
    
    /// <summary>
    /// Достает ключ ячейки из ее хэша.
    /// </summary>
    /// <param name="hash"></param>
    /// <returns>Cell's key.</returns>
    private uint GetKeyFromHash(uint hash)
    {
        return hash % (uint)_spatialLookup.Length;
    }
}