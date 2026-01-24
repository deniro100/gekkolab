using GekkoLab.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace GekkoLab.Services.GekkoDetector;

/// <summary>
/// Interface for gecko detection service
/// </summary>
public interface IGekkoDetector : IDisposable
{
    /// <summary>
    /// Detect if there's a gecko in the image
    /// </summary>
    Task<GekkoDetectionResult> DetectAsync(byte[] imageData, string imagePath);
    
    /// <summary>
    /// Check if the model is loaded and ready
    /// </summary>
    bool IsModelLoaded { get; }
}

/// <summary>
/// ONNX-based gecko detector service
/// Uses a pre-trained model to detect geckos in images
/// </summary>
public class OnnxGekkoDetector : IGekkoDetector
{
    private readonly ILogger<OnnxGekkoDetector> _logger;
    private readonly IConfiguration _configuration;
    private InferenceSession? _session;
    private readonly string _modelPath;
    private readonly int _inputWidth;
    private readonly int _inputHeight;
    private readonly float _confidenceThreshold;
    private readonly string[] _labels;
    private bool _disposed;

    public bool IsModelLoaded => _session != null;

    public OnnxGekkoDetector(
        ILogger<OnnxGekkoDetector> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        
        _modelPath = _configuration.GetValue<string>("GekkoDetector:ModelPath", "models/gekko_detector.onnx")!;
        _inputWidth = _configuration.GetValue<int>("GekkoDetector:InputWidth", 224);
        _inputHeight = _configuration.GetValue<int>("GekkoDetector:InputHeight", 224);
        _confidenceThreshold = _configuration.GetValue<float>("GekkoDetector:ConfidenceThreshold", 0.5f);
        
        // Labels for classification (can be customized based on the model)
        var labelsConfig = _configuration.GetSection("GekkoDetector:Labels").Get<string[]>();
        _labels = labelsConfig ?? new[] { "no_gecko", "gecko" };
        
        LoadModel();
    }

    private void LoadModel()
    {
        try
        {
            if (!File.Exists(_modelPath))
            {
                _logger.LogWarning("ONNX model not found at {ModelPath}. Gecko detection will not work.", _modelPath);
                return;
            }

            var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions();
            sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            
            _session = new InferenceSession(_modelPath, sessionOptions);
            
            _logger.LogInformation("ONNX model loaded successfully from {ModelPath}", _modelPath);
            _logger.LogInformation("Model inputs: {Inputs}", string.Join(", ", _session.InputMetadata.Keys));
            _logger.LogInformation("Model outputs: {Outputs}", string.Join(", ", _session.OutputMetadata.Keys));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load ONNX model from {ModelPath}", _modelPath);
        }
    }

    public async Task<GekkoDetectionResult> DetectAsync(byte[] imageData, string imagePath)
    {
        var result = new GekkoDetectionResult
        {
            Timestamp = DateTime.UtcNow,
            ImagePath = imagePath,
            GekkoDetected = false,
            Confidence = 0
        };

        if (_session == null)
        {
            _logger.LogWarning("ONNX model not loaded, skipping detection");
            return result;
        }

        try
        {
            // Preprocess image
            var inputTensor = await PreprocessImageAsync(imageData);
            
            // Get input name from model
            var inputName = _session.InputMetadata.Keys.First();
            
            // Create input container
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
            };

            // Run inference
            using var outputs = _session.Run(inputs);
            
            // Process output
            var outputTensor = outputs.First().AsTensor<float>();
            var predictions = outputTensor.ToArray();
            
            // Apply softmax if needed (for classification models)
            var probabilities = Softmax(predictions);
            
            // Find the class with highest probability
            var maxIndex = 0;
            var maxProb = probabilities[0];
            for (int i = 1; i < probabilities.Length; i++)
            {
                if (probabilities[i] > maxProb)
                {
                    maxProb = probabilities[i];
                    maxIndex = i;
                }
            }

            result.Confidence = maxProb;
            result.Label = maxIndex < _labels.Length ? _labels[maxIndex] : $"class_{maxIndex}";
            
            // Check if gecko is detected (assuming "gecko" label or index 1)
            result.GekkoDetected = result.Label?.ToLower().Contains("gecko") == true 
                                   && result.Confidence >= _confidenceThreshold;

            _logger.LogDebug("Detection result: {Label} with confidence {Confidence:P2}, GekkoDetected: {Detected}",
                result.Label, result.Confidence, result.GekkoDetected);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during gecko detection");
            return result;
        }
    }

    private async Task<DenseTensor<float>> PreprocessImageAsync(byte[] imageData)
    {
        return await Task.Run(() =>
        {
            using var image = Image.Load<Rgb24>(imageData);
            
            // Resize to model input size
            image.Mutate(x => x.Resize(_inputWidth, _inputHeight));
            
            // Create tensor with shape [1, 3, height, width] (NCHW format)
            var tensor = new DenseTensor<float>(new[] { 1, 3, _inputHeight, _inputWidth });
            
            // Normalize and fill tensor
            for (int y = 0; y < _inputHeight; y++)
            {
                for (int x = 0; x < _inputWidth; x++)
                {
                    var pixel = image[x, y];
                    
                    // Normalize to [0, 1] and then apply ImageNet normalization
                    // Mean: [0.485, 0.456, 0.406], Std: [0.229, 0.224, 0.225]
                    tensor[0, 0, y, x] = (pixel.R / 255f - 0.485f) / 0.229f; // R channel
                    tensor[0, 1, y, x] = (pixel.G / 255f - 0.456f) / 0.224f; // G channel
                    tensor[0, 2, y, x] = (pixel.B / 255f - 0.406f) / 0.225f; // B channel
                }
            }

            return tensor;
        });
    }

    private static float[] Softmax(float[] input)
    {
        var max = input.Max();
        var exp = input.Select(x => Math.Exp(x - max)).ToArray();
        var sum = exp.Sum();
        return exp.Select(x => (float)(x / sum)).ToArray();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _session?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Simulator detector for development/testing when ONNX model is not available
/// </summary>
public class SimulatorGekkoDetector : IGekkoDetector
{
    private readonly ILogger<SimulatorGekkoDetector> _logger;
    private readonly Random _random = new();

    public bool IsModelLoaded => true;

    public SimulatorGekkoDetector(ILogger<SimulatorGekkoDetector> logger)
    {
        _logger = logger;
        _logger.LogInformation("Using simulator gecko detector");
    }

    public Task<GekkoDetectionResult> DetectAsync(byte[] imageData, string imagePath)
    {
        // Simulate detection with random results
        var confidence = (float)_random.NextDouble();
        var detected = confidence > 0.7f; // 30% chance of "detecting" a gecko

        var result = new GekkoDetectionResult
        {
            Timestamp = DateTime.UtcNow,
            ImagePath = imagePath,
            GekkoDetected = detected,
            Confidence = detected ? confidence : 1 - confidence,
            Label = detected ? "gecko" : "no_gecko"
        };

        _logger.LogDebug("Simulated detection: {Label} with confidence {Confidence:P2}", 
            result.Label, result.Confidence);

        return Task.FromResult(result);
    }

    public void Dispose() { }
}
