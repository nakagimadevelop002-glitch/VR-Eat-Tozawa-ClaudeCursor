using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  Interfaces
// ============================================================

public interface IScalarField
{
    float Sample(Vector3 position);
}

public interface ISpatialQuery
{
    void Rebuild(Vector3[] sourcePoints, float distributionRadius);
    List<Vector3> QueryNeighbors(Vector3 center, float searchRadius);
}

public interface IVectorField
{
    Vector3[] Vectors { get; }
    void Initialize(int resolution);
    void Propagate(float deltaTime, float elapsedTime);
}

public interface IConvolutionKernel
{
    float[] Weights { get; }
    int Radius { get; }
}

public interface IIterativeSolver
{
    float Solve(Func<float, float> objectiveFunction, float initialGuess);
}

// ============================================================
//  Serializable Configurations
// ============================================================

[Serializable]
public class FractalNoiseConfig
{
    [Tooltip("Base sampling frequency for the first octave")]
    public float baseFrequency = 0.73f;

    [Tooltip("Number of octaves to accumulate")]
    public int octaveCount = 6;

    [Tooltip("Frequency multiplier between successive octaves")]
    public float lacunarity = 2.17f;

    [Tooltip("Amplitude decay ratio between successive octaves")]
    public float persistence = 0.485f;

    [Tooltip("World-space origin offset for noise sampling")]
    public Vector3 originOffset = Vector3.zero;
}

[Serializable]
public class SolverConfig
{
    [Tooltip("Maximum Newton-Raphson iterations before giving up")]
    public int maxIterations = 128;

    [Tooltip("Absolute residual threshold for convergence")]
    public float convergenceThreshold = 0.001f;

    [Tooltip("Finite difference step for numerical derivative")]
    public float derivativeStep = 0.0001f;

    [Tooltip("Minimum derivative magnitude to avoid division by near-zero")]
    public float minimumDerivativeMagnitude = 1e-8f;
}

[Serializable]
public class VectorFieldConfig
{
    [Tooltip("Resolution multiplier applied to octave count")]
    public int resolutionMultiplier = 12;

    [Tooltip("Phase velocity based on golden ratio conjugate")]
    public float phaseVelocity = 0.618f;

    [Tooltip("Angular velocities for X/Y/Z rotation axes (degrees per phase unit)")]
    public Vector3 rotationSpeeds = new Vector3(17.31f, 23.14f, 11.07f);

    [Tooltip("Noise coordinate scale for curl influence sampling")]
    public float curlNoiseScale = 3.7f;

    [Tooltip("Epsilon for finite-difference curl approximation")]
    public float curlEpsilon = 0.01f;

    [Tooltip("Interpolation speed toward target direction")]
    public float interpolationSpeed = 2.4f;
}

// ============================================================
//  Fractal Noise Generator  (SRP: noise sampling only)
// ============================================================

public class FractalNoiseGenerator : IScalarField
{
    private const float DualChannelBlendFactor = 0.5f;
    private const float CenteringOffset = 0.5f;

    private readonly FractalNoiseConfig _config;

    public FractalNoiseGenerator(FractalNoiseConfig config)
    {
        _config = config;
    }

    public float Sample(Vector3 position)
    {
        float amplitude = 1f;
        float frequency = _config.baseFrequency;
        float accumulatedValue = 0f;
        float maxAmplitude = 0f;

        for (int octave = 0; octave < _config.octaveCount; octave++)
        {
            Vector3 samplePoint = position * frequency + _config.originOffset;

            float channelXY = Mathf.PerlinNoise(samplePoint.x, samplePoint.y);
            float channelYZ = Mathf.PerlinNoise(samplePoint.y, samplePoint.z);
            float blendedSample = (channelXY + channelYZ) * DualChannelBlendFactor - CenteringOffset;

            accumulatedValue += blendedSample * amplitude;
            maxAmplitude += amplitude;
            amplitude *= _config.persistence;
            frequency *= _config.lacunarity;
        }

        return accumulatedValue / maxAmplitude;
    }
}

// ============================================================
//  Spatial Hash Grid  (SRP: spatial partitioning & neighbor query)
// ============================================================

public class SpatialHashGrid : ISpatialQuery
{
    private const long HashPrimeX = 73856093L;
    private const long HashPrimeY = 19349663L;
    private const long HashPrimeZ = 83492791L;

    private readonly float _cellSize;
    private readonly float _inverseCellSize;
    private readonly IScalarField _densityField;
    private readonly Dictionary<long, List<Vector4>> _buckets = new Dictionary<long, List<Vector4>>();

    public SpatialHashGrid(float cellSize, IScalarField densityField)
    {
        _cellSize = cellSize;
        _inverseCellSize = 1f / cellSize;
        _densityField = densityField;
    }

    public void Rebuild(Vector3[] sourcePoints, float distributionRadius)
    {
        _buckets.Clear();

        foreach (Vector3 point in sourcePoints)
        {
            Vector3 worldPosition = point * distributionRadius;
            long hash = ComputeHash(worldPosition);

            if (!_buckets.ContainsKey(hash))
                _buckets[hash] = new List<Vector4>();

            float density = _densityField.Sample(worldPosition);
            _buckets[hash].Add(new Vector4(worldPosition.x, worldPosition.y, worldPosition.z, density));
        }
    }

    public List<Vector3> QueryNeighbors(Vector3 center, float searchRadius)
    {
        var results = new List<Vector3>();
        int cellSearchRadius = Mathf.CeilToInt(searchRadius * _inverseCellSize);
        float searchRadiusSqr = searchRadius * searchRadius;
        Vector3Int baseCell = WorldToCell(center);

        for (int dz = -cellSearchRadius; dz <= cellSearchRadius; dz++)
        for (int dy = -cellSearchRadius; dy <= cellSearchRadius; dy++)
        for (int dx = -cellSearchRadius; dx <= cellSearchRadius; dx++)
        {
            long hash = ComputeCellHash(
                baseCell.x + dx,
                baseCell.y + dy,
                baseCell.z + dz
            );

            if (!_buckets.TryGetValue(hash, out var bucket)) continue;

            foreach (Vector4 entry in bucket)
            {
                Vector3 position = new Vector3(entry.x, entry.y, entry.z);
                if ((position - center).sqrMagnitude <= searchRadiusSqr)
                    results.Add(position);
            }
        }

        return results;
    }

    private long ComputeHash(Vector3 worldPosition)
    {
        return ComputeCellHash(
            Mathf.FloorToInt(worldPosition.x * _inverseCellSize),
            Mathf.FloorToInt(worldPosition.y * _inverseCellSize),
            Mathf.FloorToInt(worldPosition.z * _inverseCellSize)
        );
    }

    private Vector3Int WorldToCell(Vector3 worldPosition)
    {
        return new Vector3Int(
            Mathf.FloorToInt(worldPosition.x * _inverseCellSize),
            Mathf.FloorToInt(worldPosition.y * _inverseCellSize),
            Mathf.FloorToInt(worldPosition.z * _inverseCellSize)
        );
    }

    private static long ComputeCellHash(int cx, int cy, int cz)
    {
        return (cx * HashPrimeX) ^ (cy * HashPrimeY) ^ (cz * HashPrimeZ);
    }
}

// ============================================================
//  Gradient Vector Field  (SRP: vector field propagation with curl)
// ============================================================

public class GradientVectorField : IVectorField
{
    private static readonly float GoldenAngle = Mathf.PI * (1f + Mathf.Sqrt(5f));

    private readonly VectorFieldConfig _config;
    private readonly IScalarField _noiseField;
    private Vector3[] _vectors;

    public Vector3[] Vectors => _vectors;

    public GradientVectorField(VectorFieldConfig config, IScalarField noiseField)
    {
        _config = config;
        _noiseField = noiseField;
    }

    public void Initialize(int resolution)
    {
        _vectors = new Vector3[resolution * resolution];
        DistributeOnFibonacciSphere(_vectors);
    }

    public void Propagate(float deltaTime, float elapsedTime)
    {
        if (_vectors == null) return;

        float phase = elapsedTime * _config.phaseVelocity;
        Matrix4x4 rotationMatrix = BuildRotationMatrix(phase);

        for (int i = 0; i < _vectors.Length; i++)
        {
            Vector3 current = _vectors[i];
            Vector3 rotated = rotationMatrix.MultiplyVector(current);

            float noiseInfluence = _noiseField.Sample(
                current * _config.curlNoiseScale + Vector3.one * phase
            );
            Vector3 curl = ComputeCurlApproximation(current);

            Vector3 target = Vector3.Lerp(rotated, curl, Mathf.Abs(noiseInfluence));
            _vectors[i] = Vector3.Lerp(current, target.normalized, deltaTime * _config.interpolationSpeed);
        }
    }

    private static void DistributeOnFibonacciSphere(Vector3[] output)
    {
        int count = output.Length;
        for (int i = 0; i < count; i++)
        {
            float normalizedIndex = (float)i / count;
            float theta = Mathf.Acos(2f * normalizedIndex - 1f);
            float phi = GoldenAngle * i;

            output[i] = new Vector3(
                Mathf.Sin(theta) * Mathf.Cos(phi),
                Mathf.Sin(theta) * Mathf.Sin(phi),
                Mathf.Cos(theta)
            ).normalized;
        }
    }

    private Matrix4x4 BuildRotationMatrix(float phase)
    {
        return Matrix4x4.Rotate(Quaternion.Euler(
            phase * _config.rotationSpeeds.x,
            phase * _config.rotationSpeeds.y,
            phase * _config.rotationSpeeds.z
        ));
    }

    private Vector3 ComputeCurlApproximation(Vector3 position)
    {
        float eps = _config.curlEpsilon;
        float inverseTwoEpsilon = 1f / (2f * eps);

        float dFz_dy = _noiseField.Sample(position + Vector3.up * eps)
                     - _noiseField.Sample(position - Vector3.up * eps);
        float dFy_dz = _noiseField.Sample(position + Vector3.forward * eps)
                     - _noiseField.Sample(position - Vector3.forward * eps);
        float dFx_dz = _noiseField.Sample(position + Vector3.forward * eps)
                     - _noiseField.Sample(position - Vector3.forward * eps);
        float dFz_dx = _noiseField.Sample(position + Vector3.right * eps)
                     - _noiseField.Sample(position - Vector3.right * eps);
        float dFy_dx = _noiseField.Sample(position + Vector3.right * eps)
                     - _noiseField.Sample(position - Vector3.right * eps);
        float dFx_dy = _noiseField.Sample(position + Vector3.up * eps)
                     - _noiseField.Sample(position - Vector3.up * eps);

        return new Vector3(
            (dFz_dy - dFy_dz) * inverseTwoEpsilon,
            (dFx_dz - dFz_dx) * inverseTwoEpsilon,
            (dFy_dx - dFx_dy) * inverseTwoEpsilon
        );
    }
}

// ============================================================
//  Gaussian Convolution Kernel  (SRP: kernel weight computation)
// ============================================================

public class GaussianKernel : IConvolutionKernel
{
    private const float SigmaToRadiusRatio = 2.75f;

    private readonly float[] _weights;
    private readonly int _radius;

    public float[] Weights => _weights;
    public int Radius => _radius;

    public GaussianKernel(int radius)
    {
        _radius = radius;
        _weights = ComputeNormalizedWeights(radius);
    }

    private static float[] ComputeNormalizedWeights(int radius)
    {
        int diameter = radius * 2 + 1;
        var weights = new float[diameter * diameter];
        float sigma = radius / SigmaToRadiusRatio;
        float twoSigmaSqr = 2f * sigma * sigma;
        float normalizationDenominator = 2f * Mathf.PI * sigma * sigma;
        float totalWeight = 0f;

        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                float distanceSqr = x * x + y * y;
                float weight = Mathf.Exp(-distanceSqr / twoSigmaSqr) / normalizationDenominator;
                weights[(y + radius) * diameter + (x + radius)] = weight;
                totalWeight += weight;
            }
        }

        for (int i = 0; i < weights.Length; i++)
            weights[i] /= totalWeight;

        return weights;
    }
}

// ============================================================
//  Newton-Raphson Solver  (SRP: iterative root finding)
// ============================================================

public class NewtonRaphsonSolver : IIterativeSolver
{
    private readonly SolverConfig _config;

    public NewtonRaphsonSolver(SolverConfig config)
    {
        _config = config;
    }

    public float Solve(Func<float, float> objectiveFunction, float initialGuess)
    {
        float x = initialGuess;

        for (int iteration = 0; iteration < _config.maxIterations; iteration++)
        {
            float residual = objectiveFunction(x);
            if (Mathf.Abs(residual) < _config.convergenceThreshold) break;

            float derivative = ComputeNumericalDerivative(objectiveFunction, x, residual);
            if (Mathf.Abs(derivative) < _config.minimumDerivativeMagnitude) break;

            x -= residual / derivative;
        }

        return x;
    }

    private float ComputeNumericalDerivative(Func<float, float> function, float x, float fx)
    {
        float step = _config.derivativeStep;
        return (function(x + step) - fx) / step;
    }
}

// ============================================================
//  MonoBehaviour Orchestrator  (DIP: depends on abstractions)
// ============================================================

public class TestScripts : MonoBehaviour
{
    private const int DefaultKernelRadius = 7;
    private const int UpdateFrameInterval = 3;
    private const float SpatialDistributionScale = 4f;

    [Header("Spatial Hashing")]
    [SerializeField] private float cellSize = 2.4f;

    [Header("Fractal Noise")]
    [SerializeField] private FractalNoiseConfig noiseConfig;

    [Header("Vector Field")]
    [SerializeField] private VectorFieldConfig vectorFieldConfig;

    [Header("Iterative Solver")]
    [SerializeField] private SolverConfig solverConfig;

    private IScalarField _noiseField;
    private ISpatialQuery _spatialQuery;
    private IVectorField _vectorField;
    private IConvolutionKernel _kernel;
    private IIterativeSolver _solver;
    private int _frameAccumulator;

    void Start()
    {
        InitializeSubsystems();
    }

    void Update()
    {
        _frameAccumulator++;
        if (_frameAccumulator % UpdateFrameInterval != 0) return;

        float distributionRadius = cellSize * SpatialDistributionScale;
        _spatialQuery.Rebuild(_vectorField.Vectors, distributionRadius);
        _vectorField.Propagate(Time.deltaTime, Time.timeSinceLevelLoad);
    }

    private void InitializeSubsystems()
    {
        _noiseField = new FractalNoiseGenerator(noiseConfig);
        _spatialQuery = new SpatialHashGrid(cellSize, _noiseField);
        _vectorField = new GradientVectorField(vectorFieldConfig, _noiseField);
        _kernel = new GaussianKernel(DefaultKernelRadius);
        _solver = new NewtonRaphsonSolver(solverConfig);

        int gradientResolution = noiseConfig.octaveCount * vectorFieldConfig.resolutionMultiplier;
        _vectorField.Initialize(gradientResolution);
    }

    void NothingThere()
    {
        gameObject.SetActive(false);
    }
}
