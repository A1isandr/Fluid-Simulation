using System;
using System.Threading.Tasks;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Random = System.Random;
using Vector3 = UnityEngine.Vector3;
using static UnityEngine.Mathf;


public class FluidSimulation : MonoBehaviour
{
	[Header("General Settings")]
	[SerializeField] private int numParticles = 5;
	
	[Header("Simulation Settings")]
	[SerializeField] private float timeScale = 1;
	[SerializeField] private bool fixedTimeStep;
	[SerializeField] private int iterationsPerFrame = 2;
	[SerializeField] private float gravity = 9.81f;
	[SerializeField] private float smoothingRadius = 0.5f;
	[SerializeField] private float targetDensity = 5f;
	[SerializeField] private float pressureMultiplier = 30f;
	[SerializeField] private float nearPressureMultiplier = 1f;
	[SerializeField] private float viscosityStrength = 0.5f;
	[Range(0, 1)] [SerializeField] private float collisionDamping = 0.95f;
	
	[Header("References")]
	[SerializeField] private GameObject particlePrefab;

	[Header("Debug")]
	[SerializeField] private bool displayNeighbourSearchGrid;
	[SerializeField] private bool displaySmoothingRadius;
	[SerializeField] private Color gridColor = Color.yellow;
	[SerializeField] private Color radiusColor = Color.red;
	
	[Header("Debug Info")]
	[SerializeField] private GameObject[] particles;
	[SerializeField] private Vector3[] velocities;
	[SerializeField] private Vector3[] positions;
	[SerializeField] private Vector3[] predictedPositions;
	private (float density, float nearDensity)[] _densities;
	
	private NeighbourSearch _neighbourSearch;
	
	// State.
	private bool _isPaused;
	private bool _pauseNextFrame;

	private void Start()
	{
		float deltaTime = 1 / 60f;
		Time.fixedDeltaTime = deltaTime;
		
		SpawnParticles();
	}
	
	void FixedUpdate()
	{
		// Run simulation if in fixed time step mode.
		if (fixedTimeStep)
		{
			RunSimulationFrame(Time.fixedDeltaTime);
		}
	}
	
	private void Update()
	{
		// Run simulation if not in fixed time step mode.
		// (skip running for first few frames as time step can be a lot higher than usual).
		if (!fixedTimeStep)
		{
			RunSimulationFrame(Time.deltaTime);
		}

		if (_pauseNextFrame)
		{
			_isPaused = true;
			_pauseNextFrame = false;
		}

		HandleInput();
	}
	
	void RunSimulationFrame(float frameTime)
	{
		if (!_isPaused)
		{
			float timeStep = frameTime / iterationsPerFrame * timeScale;

			for (int i = 0; i < iterationsPerFrame; i++)
			{
				RunSimulationStep(timeStep);
				DrawParticles();
			}
		}
	}
	
	/// <summary>
	/// Performs simulation step every frame.
	/// </summary>
	/// <param name="deltaTime"></param>
	private void RunSimulationStep(float deltaTime)
	{
		// Apply gravity and predict next positions.
		Parallel.For(0, numParticles, i =>
		{
			velocities[i] += Vector3.down * (gravity * deltaTime);
			predictedPositions[i] = positions[i] + velocities[i] * 1 / 120f;
		});
		
		// Update spatial lookup with predicted positions;
		_neighbourSearch.UpdateSpatialLookup(predictedPositions, smoothingRadius);
		
		// Calculate densities.
		Parallel.For(0, numParticles, i =>
		{
			_densities[i] = CalculateDensity(predictedPositions[i]);
		});
		
		// Calculate and apply pressure forces.
		Parallel.For(0, numParticles, i =>
		{
			Vector3 pressureForce = CalculatePressureForce(i);
			Vector3 pressureAcceleration = pressureForce / _densities[i].density;
			velocities[i] += pressureAcceleration * deltaTime;
		});
		
		// Calculate viscosity.
		Parallel.For(0, numParticles, i =>
		{
			Vector3 viscosityForce = CalculateViscosityForce(i);
			velocities[i] += viscosityForce * deltaTime;
		});
		
		// Calculate new positions of the particles and resolve collisions.
		Parallel.For(0, numParticles, i =>
		{
			positions[i] += velocities[i] * deltaTime;
		});
		
		for (int i = 0; i < numParticles; i++)
		{
			ResolveCollisions(i);
		}
	}
	
	/// <summary>
	/// Spawns particles.
	/// </summary>
	private void SpawnParticles()
	{
		particles = new GameObject[numParticles];
		velocities = new Vector3[numParticles];
		positions = new Vector3[numParticles];
		predictedPositions = new Vector3[numParticles];
		_densities = new (float density, float nearDensity)[numParticles];
		
		_neighbourSearch = new NeighbourSearch(numParticles);
		
		float particleRadius = particlePrefab.transform.lossyScale.x;
		
		int cubeLength = (int)Math.Ceiling(Pow(numParticles, 1.0f / 3.0f)); 
		float spaceBetweenParticles = particleRadius + 1.0f;

		for (int i = 0; i < numParticles; i++)
		{
			float x = (i % cubeLength - cubeLength / 2f) * spaceBetweenParticles;
			float y = (i / cubeLength % cubeLength - cubeLength / 2f) * spaceBetweenParticles;
			float z = (i / (cubeLength * cubeLength) - cubeLength / 2f) * spaceBetweenParticles;

			positions[i] = new Vector3(x, y, z);
			particles[i] = Instantiate(particlePrefab, positions[i], Quaternion.identity);
		};
		
		_neighbourSearch.UpdateSpatialLookup(positions, smoothingRadius);

		Parallel.For(0, numParticles, i =>
		{
			_densities[i] = CalculateDensity(positions[i]);
		});
	}
	
	/// <summary>
	/// Applies new positions to the particles.
	/// </summary>
	private void DrawParticles()
	{
		for (int i = 0; i < numParticles; i++)
		{
			particles[i].transform.position = positions[i];
		}
	}
	
	void HandleInput()
	{
		// Pause.
		if (Input.GetKeyDown(KeyCode.Space))
		{
			_isPaused = !_isPaused;
		}
		
		// Frame by frame.
		if (Input.GetKeyDown(KeyCode.RightArrow))
		{
			_isPaused = false;
			_pauseNextFrame = true;
		}

		// Reset simulation.
		if (Input.GetKeyDown(KeyCode.R))
		{
			_isPaused = true;
		}
	}

	/// <summary>
	/// Resolves particle collision with boundaries of the box.
	/// </summary>
	/// <param name="particleIndex"></param>
	private void ResolveCollisions(int particleIndex)
	{
		Vector3 posLocal = transform.InverseTransformPoint(positions[particleIndex]);
		Vector3 velocityLocal = transform.InverseTransformDirection(velocities[particleIndex]);

		// Calculate distance from box on each axis (negative values are inside box)
		Vector3 halfSize = new Vector3(0.5f, 0.5f, 0.5f);
		Vector3 edgeDst = halfSize - new Vector3(Abs(posLocal.x), Abs(posLocal.y), Abs(posLocal.z));

		// Resolve collisions
		if (edgeDst.x <= 0)
		{
			posLocal.x = halfSize.x * Sign(posLocal.x);
			velocityLocal.x *= -1 * collisionDamping;
		}
		if (edgeDst.y <= 0)
		{
			posLocal.y = halfSize.y * Sign(posLocal.y);
			velocityLocal.y *= -1 * collisionDamping;
		}
		if (edgeDst.z <= 0)
		{
			posLocal.z = halfSize.z * Sign(posLocal.z);
			velocityLocal.z *= -1 * collisionDamping;
		}

		// Transform resolved position/velocity back to world space
		positions[particleIndex] = transform.TransformPoint(posLocal);
		velocities[particleIndex] = transform.TransformDirection(velocityLocal);
	}
	
	/// <summary>
	/// Smoothing kernel.
	/// </summary>
	/// <param name="radius"></param>
	/// <param name="dst"></param>
	/// <returns></returns>
	private float DensitySmoothingKernel(float dst, float radius)
	{
		//if (dst > radius) return 0;
		
		float scale = 15 / (2 * PI * Pow(radius, 5));
		float v = radius - dst;
		
		return v * v * scale;
	}
	
	/// <summary>
	/// Smoothing kernel derivative.
	/// </summary>
	/// <param name="dst"></param>
	/// <param name="radius"></param>
	/// <returns></returns>
	private float DensitySmoothingKernelDerivative(float dst, float radius)
	{
		float scale = 15 / (Pow(radius, 5) * PI);
		float v = radius - dst;
		return -v * scale;
	}
	
	/// <summary>
	/// 
	/// </summary>
	/// <param name="dst"></param>
	/// <param name="radius"></param>
	/// <returns></returns>
	private float NearDensitySmoothingKernel(float dst, float radius)
	{
		float scale = 15 / (PI * Pow(radius, 6));
		float v = radius - dst;
		return v * v * v * scale;
	}
	
	/// <summary>
	/// 
	/// </summary>
	/// <param name="dst"></param>
	/// <param name="radius"></param>
	/// <returns></returns>
	private float NearDensitySmoothingKernelDerivative(float dst, float radius)
	{
		float scale = 45 / (Pow(radius, 6) * PI);
		float v = radius - dst;
		return -v * v * scale;
	}
	
	/// <summary>
	/// 
	/// </summary>
	/// <param name="dst"></param>
	/// <param name="radius"></param>
	/// <returns></returns>
	private float ViscositySmoothingKernel(float dst, float radius)
	{
		float volume = PI * Pow(radius, 8) / 4;
		float value = Max(0, radius * radius - dst * dst);

		return value * value * value / volume;
	}
	
	/// <summary>
	/// Calculates density at given point in space.
	/// </summary>
	/// <param name="samplePoint"></param>
	/// <returns>Density</returns>
	private (float, float) CalculateDensity(Vector3 samplePoint)
	{
		float density = 0;
		float nearDensity = 0;

		foreach (var index in _neighbourSearch.ForeachPointWithinRadius(samplePoint))
		{
			float dst = (positions[index] - samplePoint).magnitude;
			float influence = DensitySmoothingKernel(dst, smoothingRadius);
			float nearInfluence = NearDensitySmoothingKernel(dst, smoothingRadius);
			
			density += influence;
			nearDensity += nearInfluence;
		}
		
		return (density, nearDensity);
	}
	
	/// <summary>
	/// Calculates pressure force acting on a given particle.
	/// </summary>
	/// <param name="particleIndex"></param>
	/// <returns>Pressure force</returns>
	private Vector3 CalculatePressureForce(int particleIndex)
	{
		Vector3 pressureForce = Vector3.zero;
		
		foreach (var index in _neighbourSearch.ForeachPointWithinRadius(predictedPositions[particleIndex]))
		{
			if (particleIndex == index) continue;
			
			Vector3 offset = positions[index] - positions[particleIndex];
			float dst = offset.magnitude;
			Vector3 dir = dst == 0 ? GetRandomDir() : offset / dst;
			
			float slope = DensitySmoothingKernelDerivative(dst, smoothingRadius);
			float nearSlope = NearDensitySmoothingKernelDerivative(dst, smoothingRadius);
			
			var densities = _densities[index];
			
			(float sharedPressure, float nearSharedPressure) = CalculateSharedPressure(densities, _densities[particleIndex]);
			
			pressureForce += dir * (sharedPressure * slope) / densities.density;
			pressureForce += dir * (nearSharedPressure * nearSlope) / densities.nearDensity;
		}
		
		return pressureForce;
	}

	private Vector3 CalculateViscosityForce(int particleIndex)
	{
		Vector3 viscosityForce = Vector3.zero;
		Vector3 position = positions[particleIndex];

		foreach (int otherIndex in _neighbourSearch.ForeachPointWithinRadius(position))
		{
			float dst = (position - positions[otherIndex]).magnitude;
			float influence = ViscositySmoothingKernel(dst, smoothingRadius);
			viscosityForce += (velocities[otherIndex] - velocities[particleIndex]) * influence;
		}

		return viscosityForce * viscosityStrength;
	}

	/// <summary>
	/// Calculates shared pressure acting on two points in space.
	/// </summary>
	/// <param name="densitiesA"></param>
	/// <param name="densitiesB"></param>
	/// <returns>Shared pressure</returns>
	private (float, float) CalculateSharedPressure((float density, float nearDensity) densitiesA, (float density, float nearDensity) densitiesB)
	{
		(float pressureA, float nearPressureA) = ConvertDensityToPressure(densitiesA.density, densitiesA.nearDensity);
		(float pressureB, float nearPressureB) = ConvertDensityToPressure(densitiesB.density, densitiesB.nearDensity);

		return ((pressureA + pressureB) / 2, nearPressureA + nearPressureB / 2);
	}

	/// <summary>
	/// Converts density to pressure at point in space.
	/// </summary>
	/// <param name="density"></param>
	/// <param name="nearDensity"></param>
	/// <returns>Pressure</returns>
	private (float, float) ConvertDensityToPressure(float density, float nearDensity)
	{
		float densityError = density - targetDensity;
		float pressure = densityError * pressureMultiplier;
		float nearPressure = nearDensity * nearPressureMultiplier;

		return (pressure, nearPressure);
	}
	
	/// <summary>
	/// Chooses random direction for particle.
	/// </summary>
	/// <returns>Random direction.</returns>
	private static Vector3 GetRandomDir()
	{
		var rng = new Random();
		var value = rng.NextDouble();

		return value switch
		{
			<= 1 / 6.0f => Vector3.down,
			<= 1 / 5.0f => Vector3.up,
			<= 1 / 4.0f => Vector3.left,
			<= 1 / 3.0f => Vector3.right,
			<= 1 / 2.0f => Vector3.back,
			_ => Vector3.forward
		};
	}

	private void OnDrawGizmos()
	{
		// Draw box boundaries.
		var m = Gizmos.matrix;
		Gizmos.matrix = transform.localToWorldMatrix;
		Gizmos.color = new Color(0, 1, 0, 0.5f);
		Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
		Gizmos.matrix = m;
		
		if (_neighbourSearch is null) return;
		
		// Draw neighbour search cells and smoothing radius.
		for (int i = 0; i < numParticles; i++)
		{
			if (displayNeighbourSearchGrid)
			{
				Gizmos.color = gridColor;
				Gizmos.DrawWireCube(_neighbourSearch.cellsCoord[i] * smoothingRadius, new Vector3(smoothingRadius, smoothingRadius, smoothingRadius));
			}

			if (displaySmoothingRadius)
			{
				Gizmos.color = radiusColor;
				Gizmos.DrawWireSphere(positions[i], smoothingRadius);
			}
		}
	}
}
