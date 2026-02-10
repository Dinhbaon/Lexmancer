using Godot;
using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Lexmancer.Services;

/// <summary>
/// Detects NVIDIA GPU presence and capabilities for LLM optimization.
/// Auto-configures GPU layer count based on available VRAM.
/// </summary>
public static class GpuDetector
{
	/// <summary>
	/// Detect NVIDIA GPU and return recommended configuration.
	/// Returns null if no compatible GPU detected (falls back to CPU).
	/// </summary>
	public static GpuInfo DetectNvidiaGpu()
	{
		// Method 1: Check CUDA environment variables
		var cudaPath = System.Environment.GetEnvironmentVariable("CUDA_PATH");
		var cudaHome = System.Environment.GetEnvironmentVariable("CUDA_HOME");

		if (string.IsNullOrEmpty(cudaPath) && string.IsNullOrEmpty(cudaHome))
		{
			GD.Print("No CUDA environment variables found - attempting nvidia-smi detection");
		}
		else
		{
			GD.Print($"CUDA detected: {cudaPath ?? cudaHome}");
		}

		// Method 2: Use nvidia-smi to get GPU info (works on Linux/Windows)
		var gpuInfo = QueryNvidiaSmi();
		if (gpuInfo != null)
		{
			GD.Print($"NVIDIA GPU detected: {gpuInfo.Name}");
			GD.Print($"  VRAM: {gpuInfo.VramMB} MB ({gpuInfo.VramGB:F1} GB)");
			GD.Print($"  Recommended GPU layers: {gpuInfo.RecommendedGpuLayers}");
			GD.Print($"  Recommended threads: {gpuInfo.RecommendedThreads}");
			return gpuInfo;
		}

		GD.Print("No NVIDIA GPU detected - using CPU-only mode");
		return null;
	}

	/// <summary>
	/// Query nvidia-smi for GPU information.
	/// Returns null if nvidia-smi not available or fails.
	/// </summary>
	private static GpuInfo QueryNvidiaSmi()
	{
		try
		{
			var process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = "nvidia-smi",
					Arguments = "--query-gpu=name,memory.total --format=csv,noheader,nounits",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true
				}
			};

			process.Start();
			string output = process.StandardOutput.ReadToEnd();
			process.WaitForExit();

			if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
			{
				return null;
			}

			// Parse output: "NVIDIA GeForce RTX 3060, 12288"
			var lines = output.Trim().Split('\n');
			if (lines.Length == 0)
				return null;

			var parts = lines[0].Split(',');
			if (parts.Length < 2)
				return null;

			var name = parts[0].Trim();
			if (!int.TryParse(parts[1].Trim(), out int vramMB))
				return null;

			return new GpuInfo
			{
				Name = name,
				VramMB = vramMB,
				VramGB = vramMB / 1024.0,
				RecommendedGpuLayers = CalculateOptimalGpuLayers(vramMB),
				RecommendedThreads = CalculateOptimalThreads(vramMB)
			};
		}
		catch (Exception ex)
		{
			GD.Print($"Failed to query nvidia-smi: {ex.Message}");
			return null;
		}
	}

	/// <summary>
	/// Calculate optimal GPU layer count based on available VRAM.
	/// Tuned for granite-3.1-3b-a800m (1.9GB model + ~1GB KV cache = ~3GB total).
	/// </summary>
	private static int CalculateOptimalGpuLayers(int vramMB)
	{
		double vramGB = vramMB / 1024.0;

		// Optimized for granite-3.1-3b-a800m Q4_K_M (~1.9GB weights + 1GB KV cache)
		// This 800M active parameter model is very small and GPU-friendly
		if (vramGB >= 4.0)
			return 99; // Full offload - plenty of headroom (4GB > 3GB needed)
		else if (vramGB >= 3.0)
			return 99; // Full offload - tight fit but works
		else if (vramGB >= 2.5)
			return 50; // Partial offload - split between GPU/CPU
		else if (vramGB >= 2.0)
			return 25; // Light offload - mostly CPU
		else
			return 0; // Too little VRAM, use CPU only
	}

	/// <summary>
	/// Calculate optimal CPU thread count when GPU is active.
	/// Reduce CPU threads to avoid competing with GPU inference.
	/// </summary>
	private static int CalculateOptimalThreads(int vramMB)
	{
		double vramGB = vramMB / 1024.0;

		// When GPU is doing most of the work, reduce CPU thread count
		if (vramGB >= 3.0)
			return 4; // Full GPU offload - minimal CPU usage
		else if (vramGB >= 2.0)
			return 6; // Partial offload - balanced CPU/GPU
		else
			return 8; // Light offload - more CPU threads for hybrid
	}
}

/// <summary>
/// GPU information and recommended settings.
/// </summary>
public class GpuInfo
{
	public string Name { get; set; }
	public int VramMB { get; set; }
	public double VramGB { get; set; }
	public int RecommendedGpuLayers { get; set; }
	public int RecommendedThreads { get; set; }
}
