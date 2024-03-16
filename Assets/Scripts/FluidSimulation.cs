using System;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using Quaternion = UnityEngine.Quaternion;
using Random = System.Random;
using Vector3 = UnityEngine.Vector3;


public class FluidSimulation : MonoBehaviour
{
	[Header("General Settings")]
	[SerializeField] private int numParticles = 5;
	
	[Header("Simulation Settings")]
	[SerializeField] private float timeScale = 1;
	[SerializeField] private bool fixedTimeStep;
	[SerializeField] private int iterationsPerFrame = 2;
	[SerializeField] private float gravity = 9.81f;
	[SerializeField] private float smoothingRadius = 0.2f;
	[SerializeField] private float targetDensity = 1f;
	[SerializeField] private float pressureMultiplier = 1f;
	[SerializeField] private float mass = 1f;
	[Range(0, 1)] [SerializeField] private float collisionDamping = 0.95f;
	
	[Header("References")]
	[SerializeField] private Transform spawnPoint;
	[SerializeField] private GameObject particlePrefab;

	[Header("Debug")]
	[SerializeField] private bool displayNeighbourSearchGrid;
	[SerializeField] private bool displaySmoothingRadius;
	
	[Header("Debug Info")]
	[SerializeField] private GameObject[] particles;
	[SerializeField] private Vector3[] velocities;
	[SerializeField] private Vector3[] positions;
	[SerializeField] private Vector3[] predictedPositions;
	[SerializeField] private float[] densities;
	
	private NeighbourSearch _neighbourSearch;
	
	// State.
	private bool isPaused;
	private bool pauseNextFrame;

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
		// Run simulation if not in fixed timestep mode.
		// (skip running for first few frames as timestep can be a lot higher than usual).
		if (!fixedTimeStep && Time.frameCount > 10)
		{
			RunSimulationFrame(Time.deltaTime);
		}

		if (pauseNextFrame)
		{
			isPaused = true;
			pauseNextFrame = false;
		}

		HandleInput();
	}
	
	void RunSimulationFrame(float frameTime)
	{
		if (!isPaused)
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
			predictedPositions[i] = positions[i] + velocities[i] * deltaTime;
		});
		
		// Update spatial lookup with predicted positions;
		_neighbourSearch.UpdateSpatialLookup(predictedPositions, smoothingRadius);
		
		// Calculate densities.
		Parallel.For(0, numParticles, i =>
		{
			densities[i] = CalculateDensity(predictedPositions[i]);
		});
		
		// Calculate and apply pressure forces.
		Parallel.For(0, numParticles, i =>
		{
			Vector3 pressureForce = CalculatePressureForce(i);
			Vector3 pressureAcceleration = pressureForce / densities[i];
			velocities[i] += pressureAcceleration * deltaTime;
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
		densities = new float[numParticles];
		
		_neighbourSearch = new NeighbourSearch(numParticles);
		
		const float particleRadius = 0.5f;
		
		int cubeLength = (int)Math.Ceiling(Math.Pow(numParticles, 1.0f / 3.0f)); 
		float spaceBetweenParticles = particleRadius + 1.0f;

		for (int i = 0; i < numParticles; i++)
		{
			float x = (i % cubeLength - cubeLength / 2f) * spaceBetweenParticles;
			float y = ((i / cubeLength) % cubeLength - cubeLength / 2f) * spaceBetweenParticles;
			float z = (i / (cubeLength * cubeLength) - cubeLength / 2f) * spaceBetweenParticles;

			positions[i] = new Vector3(x, y, z) + spawnPoint.position;
			particles[i] = Instantiate(particlePrefab, positions[i], Quaternion.identity);
		};
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
			isPaused = !isPaused;
		}
		
		// Pause next frame.
		if (Input.GetKeyDown(KeyCode.RightArrow))
		{
			isPaused = false;
			pauseNextFrame = true;
		}

		// Reset simulation.
		if (Input.GetKeyDown(KeyCode.R))
		{
			isPaused = true;
		}
	}

	/// <summary>
	/// Resolves particle collision with boundaries of the box.
	/// </summary>
	/// <param name="particleIndex"></param>
	private void ResolveCollisions(int particleIndex)
	{
		Vector3 posLocal = transform.InverseTransformPoint(positions[particleIndex]);
		Vector3 velocityLocal = transform.InverseTransformPoint(velocities[particleIndex]);

		// Calculate distance from box on each axis (negative values are inside box)
		Vector3 halfSize = new Vector3(0.5f, 0.5f, 0.5f);
		Vector3 edgeDst = halfSize - new Vector3(Mathf.Abs(posLocal.x), Mathf.Abs(posLocal.y), Mathf.Abs(posLocal.z));

		// Resolve collisions
		if (edgeDst.x <= 0)
		{
			posLocal.x = halfSize.x * Mathf.Sign(posLocal.x);
			velocityLocal.x *= -1 * collisionDamping;
		}
		if (edgeDst.y <= 0)
		{
			posLocal.y = halfSize.y * Mathf.Sign(posLocal.y);
			velocityLocal.y *= -1 * collisionDamping;
		}
		if (edgeDst.z <= 0)
		{
			posLocal.z = halfSize.z * Mathf.Sign(posLocal.z);
			velocityLocal.z *= -1 * collisionDamping;
		}

		// Transform resolved position/velocity back to world space
		positions[particleIndex] = transform.TransformPoint(posLocal);
		velocities[particleIndex] = transform.TransformPoint(velocityLocal);
	}
	
	/// <summary>
	/// Smoothing kernel.
	/// </summary>
	/// <param name="radius"></param>
	/// <param name="dst"></param>
	/// <returns></returns>
	private float SmoothingKernel(float dst, float radius)
	{
		//if (dst > radius) return 0;
		
		float volume = (MathF.PI * MathF.Pow(radius, 4)) / 6;
		
		return (radius - dst) * (radius - dst) / volume;
	}
	
	/// <summary>
	/// Smoothing kernel derivative.
	/// </summary>
	/// <param name="dst"></param>
	/// <param name="radius"></param>
	/// <returns></returns>
	private float SmoothingKernelDerivative(float dst, float radius)
	{
		if (dst >= radius) return 0;
		
		float scale = 12 / (MathF.Pow(radius, 4) * MathF.PI);
		
		return (dst - radius) * scale;
	}
	
	/// <summary>
	/// Calculates density at given point in space.
	/// </summary>
	/// <param name="samplePoint"></param>
	/// <returns>Density</returns>
	private float CalculateDensity(Vector3 samplePoint)
	{
		float density = 0;

		foreach (var index in _neighbourSearch.ForeachPointWithinRadius(samplePoint))
		{
			float dst = (positions[index] - samplePoint).magnitude;
			float influence = SmoothingKernel(dst, smoothingRadius);
			density += mass * influence;
		}
		
		return density;
	}
	
	/// <summary>
	/// Calculates pressure force acting on a given particle.
	/// </summary>
	/// <param name="particleIndex"></param>
	/// <returns>Pressure force</returns>
	private Vector3 CalculatePressureForce(int particleIndex)
	{
		Vector3 pressureForce = Vector3.zero;
		
		foreach (var index in _neighbourSearch.ForeachPointWithinRadius(positions[particleIndex]))
		{
			if (particleIndex == index) continue;
			
			Vector3 offset = positions[index] - positions[particleIndex];
			float dst = offset.magnitude;
			Vector3 dir = dst == 0 ? GetRandomDir() : offset / dst;
			
			float slope = SmoothingKernelDerivative(dst, smoothingRadius);
			float density = densities[index];
			float sharedPressure = CalculateSharedPressure(density, densities[particleIndex]);
			pressureForce += dir * (sharedPressure * slope * mass) / density;
		}
		
		return pressureForce;
	}
	
	/// <summary>
	/// Converts density to pressure at point in space.
	/// </summary>
	/// <param name="density"></param>
	/// <returns>Pressure</returns>
	private float ConvertDensityToPressure(float density)
	{
		float densityError = density - targetDensity;
		float pressure = densityError * pressureMultiplier;

		return pressure;
	}

	/// <summary>
	/// Calculates shared pressure acting on two points in space.
	/// </summary>
	/// <param name="densityA"></param>
	/// <param name="densityB"></param>
	/// <returns>Shared pressure</returns>
	private float CalculateSharedPressure(float densityA, float densityB)
	{
		float pressureA = ConvertDensityToPressure(densityA);
		float pressureB = ConvertDensityToPressure(densityB);

		return (pressureA + pressureB) / 2;
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
				Gizmos.color = Color.yellow;
				Gizmos.DrawWireCube(_neighbourSearch.cellsCoord[i], new Vector3(smoothingRadius, smoothingRadius, smoothingRadius));
			}

			if (displaySmoothingRadius)
			{
				Gizmos.color = Color.red;
				Gizmos.DrawWireSphere(positions[i], smoothingRadius);
			}
		}
	}
}
