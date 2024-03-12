using System;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using Quaternion = UnityEngine.Quaternion;
using Random = System.Random;
using Vector3 = UnityEngine.Vector3;

public class FluidSimulation : MonoBehaviour
{
	[Header("General Settings")]
	[SerializeField] private int numParticles = 5;
	[SerializeField] private float spacing = 1.0f;
	
	[Header("Box Settings")]
	[SerializeField] private Material boxMaterial;
	[Range(0, 5)] [SerializeField] private float boxScaleX = 2;
	[Range(0, 5)] [SerializeField] private float boxScaleY = 1;
	[Range(0, 5)] [SerializeField] private float boxScaleZ = 2;
	//[SerializeField] private Vector3 boxScale = new(2.0f, 1.0f, 1.0f);
	
	[Header("Simulation Settings")]
	[Range(0, 1)] [SerializeField] private float collisionDumping = 0.95f;
	[SerializeField] private float gravity = 9.81f;
	[SerializeField] private float smoothingRadius = 0.5f;
	[SerializeField] private float targetDensity = 1f;
	[SerializeField] private float pressureMultiplier = 1f;
	[SerializeField] private float mass = 1f;
	
	[Header("References")]
	[SerializeField] private Transform spawnPoint;
	[SerializeField] private GameObject particlePrefab;

	[SerializeField] private GameObject[] particles;
	[SerializeField] private Vector3[] velocities;
	[SerializeField] private Vector3[] positions;
	[SerializeField] private float[] densities;
	private Vector3 _particleSize;

	private void Start()
	{
		DrawBox();
		SpawnParticles();
	}

	private void Update()
	{
		SimulationStep(Time.deltaTime);
		DrawParticles();
	}

	private void DrawBox()
	{
		Vector3 boxScale = new Vector3(boxScaleX, boxScaleY, boxScaleZ);
		
		// Floor
		GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
		floor.GetComponent<Renderer>().material = boxMaterial;
		floor.transform.position = new Vector3(0, -boxScale.y * 5, 0);
		floor.transform.localScale = boxScale;
		
		// Walls
		GameObject wall1 = GameObject.CreatePrimitive(PrimitiveType.Plane);
		wall1.GetComponent<Renderer>().material = boxMaterial;
		wall1.transform.position = new Vector3(0, 0, boxScale.x * 5);
		wall1.transform.localScale = new Vector3(boxScale.x, 1, boxScale.y);;
		wall1.transform.rotation = Quaternion.Euler(90, 180, 0);
		
		GameObject wall2 = GameObject.CreatePrimitive(PrimitiveType.Plane);
		wall2.GetComponent<Renderer>().material = boxMaterial;
		wall2.transform.position = new Vector3(0, 0, -boxScale.x * 5);
		wall2.transform.localScale = new Vector3(boxScale.x, 1, boxScale.y);
		wall2.transform.rotation = Quaternion.Euler(90, 0, 0);
		
		GameObject wall3 = GameObject.CreatePrimitive(PrimitiveType.Plane);
		wall3.GetComponent<Renderer>().material = boxMaterial;
		wall3.transform.position = new Vector3(boxScale.x * 5, 0, 0);
		wall3.transform.localScale =  new Vector3(boxScale.x, 1, boxScale.y);;
		wall3.transform.rotation = Quaternion.Euler(90, 270, 0);
		
		GameObject wall4 = GameObject.CreatePrimitive(PrimitiveType.Plane);
		wall4.GetComponent<Renderer>().material = boxMaterial;
		wall4.transform.position = new Vector3(-boxScale.x * 5, 0, 0);
		wall4.transform.localScale =  new Vector3(boxScale.x, 1, boxScale.y);;
		wall4.transform.rotation = Quaternion.Euler(90, 90, 0);
		
		// Ceiling
		GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Plane);
		ceiling.GetComponent<Renderer>().material = boxMaterial;
		ceiling.transform.position = new Vector3(0, boxScale.y * 5, 0);
		ceiling.transform.localScale = boxScale;
		ceiling.transform.rotation = Quaternion.Euler(0, 0, 180);
	}
	
	/// <summary>
	/// Spawns particles.
	/// </summary>
	private void SpawnParticles()
	{
		particles = new GameObject[numParticles];
		velocities = new Vector3[numParticles];
		positions = new Vector3[numParticles];
		densities = new float[numParticles];
		_particleSize = particlePrefab.transform.lossyScale;

		// Длина стороны куба
		int cubeLength = (int)Math.Ceiling(Math.Pow(numParticles, 1.0f / 3.0f)); 
		var spaceBetweenParticles = _particleSize.x / 2 + spacing;

		for (int i = 0; i < numParticles; i++)
		{
			float x = (i % cubeLength - cubeLength / 2f) * spaceBetweenParticles;
			float y = ((i / cubeLength) % cubeLength - cubeLength / 2f) * spaceBetweenParticles;
			float z = (i / (cubeLength * cubeLength) - cubeLength / 2f) * spaceBetweenParticles;

			positions[i] = new Vector3(x, y, z) + spawnPoint.position;
			densities[i] = CalculateDensity(positions[i]);
			particles[i] = Instantiate(particlePrefab, positions[i], Quaternion.identity);
		};
	}
	
	/// <summary>
	/// Performs simulation step every frame.
	/// </summary>
	/// <param name="deltaTime"></param>
	private void SimulationStep(float deltaTime)
	{
		Parallel.For(0, numParticles, i =>
		{
			velocities[i] += Vector3.down * (gravity * deltaTime);
			densities[i] = CalculateDensity(positions[i]);
		});
		
		Parallel.For(0, numParticles, i =>
		{
			Vector3 pressureForce = CalculatePressureForce(i);
			Vector3 pressureAcceleration = pressureForce / densities[i];
			velocities[i] += pressureAcceleration * deltaTime;
		});

		Parallel.For(0, numParticles, i =>
		{
			positions[i] += velocities[i] * deltaTime;
			(positions[i], velocities[i]) = ResolveCollisions(positions[i], velocities[i]);
		});
	}
	
	/// <summary>
	/// Draws particles every frame.
	/// </summary>
	private void DrawParticles()
	{
		for (int i = 0; i < numParticles; i++)
		{
			particles[i].transform.position = positions[i];
		}
	}
	
	/// <summary>
	/// Resolves particle collision with boundaries.
	/// </summary>
	/// <param name="position"></param>
	/// <param name="velocity"></param>
	/// <returns>New position and velocity of a particle.</returns>
	private (Vector3, Vector3) ResolveCollisions(Vector3 position, Vector3 velocity)
	{
		var radius = _particleSize / 2;
		Vector3 halfBoxSize = new Vector3(boxScaleX, boxScaleY, boxScaleZ) / 2 * 10 - radius;

		for (int i = 0; i < numParticles; i++)
		{
			if (Math.Abs(position.x) > halfBoxSize.x)
			{
				position.x = halfBoxSize.x * Math.Sign(position.x);
				velocity.x *= -1 * collisionDumping;
			}
			
			if (Math.Abs(position.y) > halfBoxSize.y)
			{
				position.y = halfBoxSize.y * Math.Sign(position.y);
				velocity.y *= -1 * collisionDumping;
			}

			if (Math.Abs(position.z) > halfBoxSize.z)
			{
				position.z = halfBoxSize.z * Math.Sign(position.z);
				velocity.z *= -1 * collisionDumping;
			}
		}

		return (position, velocity);
	}
	
	/// <summary>
	/// 
	/// </summary>
	/// <param name="radius"></param>
	/// <param name="dst"></param>
	/// <returns></returns>
	private float SmoothingKernel(float radius, float dst)
	{
		if (dst >= radius) return 0;
		
		float volume = (MathF.PI * MathF.Pow(radius, 4)) / 6;
		
		return (radius - dst) * (radius - dst) / volume;
	}
	
	/// <summary>
	/// 
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
	/// Calculates density in given point in space.
	/// </summary>
	/// <param name="samplePoint"></param>
	/// <returns>Density</returns>
	private float CalculateDensity(Vector3 samplePoint)
	{
		float density = 0;

		for (int i = 0; i < numParticles; i++)
		{
			float dst = (positions[i] - samplePoint).magnitude;
			float influence = SmoothingKernel(smoothingRadius, dst);
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
		
		for (int i = 0; i < numParticles; i++)
		{
			if (particleIndex == i) continue;
			
			Vector3 offset = positions[i] - positions[particleIndex];
			float dst = offset.magnitude;
			Vector3 dir = dst == 0 ? GetRandomDir() : offset / dst;
			
			float slope = SmoothingKernelDerivative(smoothingRadius, dst);
			float density = densities[i];
			float sharedPressure = CalculateSharedPressure(density, densities[particleIndex]);
			pressureForce += dir * (sharedPressure * slope * mass) / density;
		}

		return pressureForce;
	}
	
	/// <summary>
	/// Converts density to pressure on point in space.
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
	/// <returns>Random direction</returns>
	private static Vector3 GetRandomDir()
	{
		var rng = new Random();
		var value = rng.NextDouble();

		switch (value)
		{
			case <= 1 / 6.0f:
				return Vector3.down;
			case <= 1 / 5.0f:
				return Vector3.up;
			case <= 1 / 4.0f:
				return Vector3.left;
			case <= 1 / 3.0f:
				return Vector3.right;
			case <= 1 / 2.0f:
				return Vector3.back;
			default:
				return Vector3.forward;
		}
	}
}
