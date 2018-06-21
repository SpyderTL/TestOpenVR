using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using SharpDX.Multimedia;
using SharpDX.Windows;
using SharpDX.X3DAudio;
using SharpDX.XAudio2;
using Valve.VR;

namespace TestOpenVR
{
	static class Program
	{
		static CVRSystem system;
		static CVRCompositor compositor;
		static uint count;
		static uint headset;
		static List<uint> controllers;
		static RenderModel_t[] controllerModels;
		static RenderModel_TextureMap_t[] controllerTextures;
		static SharpDX.Direct3D11.Buffer[] controllerVertexBuffers;
		static SharpDX.Direct3D11.Buffer[] controllerIndexBuffers;
		static VertexBufferBinding[] controllerVertexBufferBindings;
		static TrackedDevicePose_t[] currentPoses;
		static TrackedDevicePose_t[] nextPoses;
		static ShaderResourceView[] controllerTextureViews;
		static Size windowSize;
		static RawColor4 backgroundColor;
		static Size headsetSize;
		static SharpDX.Direct3D11.Device device;
		static SwapChain swapChain;
		static DeviceContext context;
		static RenderTargetView backBufferView;
		static VertexShader controllerVertexShader;
		static PixelShader controllerPixelShader;
		static Matrix worldViewProjection;
		static SharpDX.Direct3D11.Buffer worldViewProjectionBuffer;
		static RasterizerState rasterizerState;
		static BlendState blendState;
		static DepthStencilState depthStencilState;
		static SamplerState samplerState;
		static DateTime startTime;
		static int frame;
		static Matrix head;
		static XAudio2 audio;
		static DepthStencilView depthStencilView;
		static X3DAudio audio3d;
		static Emitter[] controllerEmitters;
		static SourceVoice[] controllerVoices;
		static Listener listener;

		[STAThread]
		static void Main()
		{
			var initError = EVRInitError.None;

			system = OpenVR.Init(ref initError);

			if (initError != EVRInitError.None)
				return;

			compositor = OpenVR.Compositor;

			compositor.CompositorBringToFront();
			compositor.FadeGrid(5.0f, false);

			count = OpenVR.k_unMaxTrackedDeviceCount;

			currentPoses = new TrackedDevicePose_t[count];
			nextPoses = new TrackedDevicePose_t[count];

			controllers = new List<uint>();
			controllerModels = new RenderModel_t[count];
			controllerTextures = new RenderModel_TextureMap_t[count];
			controllerTextureViews = new ShaderResourceView[count];
			controllerVertexBuffers = new SharpDX.Direct3D11.Buffer[count];
			controllerIndexBuffers = new SharpDX.Direct3D11.Buffer[count];
			controllerVertexBufferBindings = new VertexBufferBinding[count];
			controllerEmitters = new Emitter[count];
			controllerVoices = new SourceVoice[count];

			for (uint device = 0; device < count; device++)
			{
				var deviceClass = system.GetTrackedDeviceClass(device);

				switch (deviceClass)
				{
					case ETrackedDeviceClass.HMD:
						headset = device;
						break;

					case ETrackedDeviceClass.Controller:
						controllers.Add(device);
						break;
				}
			}

			uint width = 0;
			uint height = 0;

			system.GetRecommendedRenderTargetSize(ref width, ref height);

			headsetSize = new Size((int)width, (int)height);
			windowSize = new Size(960, 540);

			var leftEyeProjection = Convert(system.GetProjectionMatrix(EVREye.Eye_Left, 0.01f, 1000.0f));
			var rightEyeProjection = Convert(system.GetProjectionMatrix(EVREye.Eye_Right, 0.01f, 1000.0f));

			var leftEyeView = Convert(system.GetEyeToHeadTransform(EVREye.Eye_Left));
			var rightEyeView = Convert(system.GetEyeToHeadTransform(EVREye.Eye_Right));

			foreach (var controller in controllers)
			{
				var modelName = new StringBuilder(255, 255);
				var propertyError = ETrackedPropertyError.TrackedProp_Success;

				var length = system.GetStringTrackedDeviceProperty(controller, ETrackedDeviceProperty.Prop_RenderModelName_String, modelName, 255, ref propertyError);

				if (propertyError == ETrackedPropertyError.TrackedProp_Success)
				{
					var modelName2 = modelName.ToString();

					while (true)
					{
						var pointer = IntPtr.Zero;
						var modelError = EVRRenderModelError.None;

						modelError = OpenVR.RenderModels.LoadRenderModel_Async(modelName2, ref pointer);

						if (modelError == EVRRenderModelError.Loading)
							continue;

						if (modelError == EVRRenderModelError.None)
						{
							var renderModel = System.Runtime.InteropServices.Marshal.PtrToStructure<RenderModel_t>(pointer);

							controllerModels[controller] = renderModel;
							break;
						}
					}

					while (true)
					{
						var pointer = IntPtr.Zero;
						var textureError = EVRRenderModelError.None;

						textureError = OpenVR.RenderModels.LoadTexture_Async(controllerModels[controller].diffuseTextureId, ref pointer);

						if (textureError == EVRRenderModelError.Loading)
							continue;

						if (textureError == EVRRenderModelError.None)
						{
							var texture = System.Runtime.InteropServices.Marshal.PtrToStructure<RenderModel_TextureMap_t>(pointer);

							controllerTextures[controller] = texture;
							break;
						}
					}
				}
			}

			int adapterIndex = 0;

			system.GetDXGIOutputInfo(ref adapterIndex);

			using (var form = new Form())
			using (var factory = new Factory4())
			{
				form.ClientSize = windowSize;

				var adapter = factory.GetAdapter(adapterIndex);

				var swapChainDescription = new SwapChainDescription
				{
					BufferCount = 1,
					Flags = SwapChainFlags.None,
					IsWindowed = true,
					ModeDescription = new ModeDescription
					{
						Format = Format.B8G8R8A8_UNorm,
						Width = form.ClientSize.Width,
						Height = form.ClientSize.Height,
						RefreshRate = new Rational(60, 1)
					},
					OutputHandle = form.Handle,
					SampleDescription = new SampleDescription(1, 0),
					SwapEffect = SwapEffect.Discard,
					Usage = Usage.RenderTargetOutput
				};

				SharpDX.Direct3D11.Device.CreateWithSwapChain(adapter, DeviceCreationFlags.None, swapChainDescription, out device, out swapChain);

				factory.MakeWindowAssociation(form.Handle, WindowAssociationFlags.None);

				context = device.ImmediateContext;

				using (var backBuffer = swapChain.GetBackBuffer<Texture2D>(0))
					backBufferView = new RenderTargetView(device, backBuffer);

				var depthBufferDescription = new Texture2DDescription
				{
					Format = Format.D16_UNorm,
					ArraySize = 1,
					MipLevels = 1,
					Width = form.ClientSize.Width,
					Height = form.ClientSize.Height,
					SampleDescription = new SampleDescription(1, 0),
					Usage = ResourceUsage.Default,
					BindFlags = BindFlags.DepthStencil,
					CpuAccessFlags = CpuAccessFlags.None,
					OptionFlags = ResourceOptionFlags.None
				};

				using (var depthBuffer = new Texture2D(device, depthBufferDescription))
					depthStencilView = new DepthStencilView(device, depthBuffer);

				// Create Eye Textures
				var eyeTextureDescription = new Texture2DDescription
				{
					ArraySize = 1,
					BindFlags = BindFlags.RenderTarget,
					CpuAccessFlags = CpuAccessFlags.None,
					Format = Format.B8G8R8A8_UNorm,
					Width = headsetSize.Width,
					Height = headsetSize.Height,
					MipLevels = 1,
					OptionFlags = ResourceOptionFlags.None,
					SampleDescription = new SampleDescription(1, 0),
					Usage = ResourceUsage.Default
				};

				var leftEyeTexture = new Texture2D(device, eyeTextureDescription);
				var rightEyeTexture = new Texture2D(device, eyeTextureDescription);

				var leftEyeTextureView = new RenderTargetView(device, leftEyeTexture);
				var rightEyeTextureView = new RenderTargetView(device, rightEyeTexture);

				// Create Eye Depth Buffer
				eyeTextureDescription.BindFlags = BindFlags.DepthStencil;
				eyeTextureDescription.Format = Format.D32_Float;

				var eyeDepth = new Texture2D(device, eyeTextureDescription);
				var eyeDepthView = new DepthStencilView(device, eyeDepth);

				Shapes.Cube.Load(device);
				Shapes.Sphere.Load(device);
				Shaders.Load(device);

				// Load Controller Models
				foreach (var controller in controllers)
				{
					var model = controllerModels[controller];

					controllerVertexBuffers[controller] = new SharpDX.Direct3D11.Buffer(device, model.rVertexData, new BufferDescription
					{
						BindFlags = BindFlags.VertexBuffer,
						SizeInBytes = (int)model.unVertexCount * 32
					});

					controllerVertexBufferBindings[controller] = new VertexBufferBinding(controllerVertexBuffers[controller], 32, 0);

					controllerIndexBuffers[controller] = new SharpDX.Direct3D11.Buffer(device, model.rIndexData, new BufferDescription
					{
						BindFlags = BindFlags.IndexBuffer,
						SizeInBytes = (int)model.unTriangleCount * 3 * 2
					});

					var texture = controllerTextures[controller];

					using (var texture2d = new Texture2D(device, new Texture2DDescription
					{
						ArraySize = 1,
						BindFlags = BindFlags.ShaderResource,
						Format = Format.R8G8B8A8_UNorm,
						Width = texture.unWidth,
						Height = texture.unHeight,
						MipLevels = 1,
						SampleDescription = new SampleDescription(1, 0)
					}, new DataRectangle(texture.rubTextureMapData, texture.unWidth * 4)))
						controllerTextureViews[controller] = new ShaderResourceView(device, texture2d);
				}

				var controllerVertexShaderByteCode = SharpDX.D3DCompiler.ShaderBytecode.Compile(Properties.Resources.NormalTextureShader, "VS", "vs_5_0");
				controllerVertexShader = new VertexShader(device, controllerVertexShaderByteCode);
				controllerPixelShader = new PixelShader(device, SharpDX.D3DCompiler.ShaderBytecode.Compile(Properties.Resources.NormalTextureShader, "PS", "ps_5_0"));

				var controllerLayout = new InputLayout(device, SharpDX.D3DCompiler.ShaderSignature.GetInputSignature(controllerVertexShaderByteCode), new InputElement[]
				{
					new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
					new InputElement("NORMAL", 0, Format.R32G32B32_Float, 12, 0),
					new InputElement("TEXCOORD", 0, Format.R32G32_Float, 24, 0)
				});

				worldViewProjectionBuffer = new SharpDX.Direct3D11.Buffer(device, Utilities.SizeOf<Matrix>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);

				var rasterizerStateDescription = RasterizerStateDescription.Default();
				//rasterizerStateDescription.FillMode = FillMode.Wireframe;
				rasterizerStateDescription.IsFrontCounterClockwise = true;
				//rasterizerStateDescription.CullMode = CullMode.None;

				rasterizerState = new RasterizerState(device, rasterizerStateDescription);

				var blendStateDescription = BlendStateDescription.Default();

				blendStateDescription.RenderTarget[0].BlendOperation = BlendOperation.Add;
				blendStateDescription.RenderTarget[0].SourceBlend = BlendOption.SourceAlpha;
				blendStateDescription.RenderTarget[0].DestinationBlend = BlendOption.InverseSourceAlpha;

				blendStateDescription.RenderTarget[0].IsBlendEnabled = false;

				blendState = new BlendState(device, blendStateDescription);

				var depthStateDescription = DepthStencilStateDescription.Default();

				depthStateDescription.DepthComparison = Comparison.LessEqual;
				depthStateDescription.IsDepthEnabled = true;
				depthStateDescription.IsStencilEnabled = false;

				depthStencilState = new DepthStencilState(device, depthStateDescription);

				var samplerStateDescription = SamplerStateDescription.Default();

				samplerStateDescription.Filter = Filter.MinMagMipLinear;
				samplerStateDescription.AddressU = TextureAddressMode.Wrap;
				samplerStateDescription.AddressV = TextureAddressMode.Wrap;

				samplerState = new SamplerState(device, samplerStateDescription);

				startTime = DateTime.Now;
				frame = 0;
				windowSize = form.ClientSize;

				backgroundColor = new RawColor4(0.1f, 0.1f, 0.1f, 1);

				var vrEvent = new VREvent_t();
				var eventSize = (uint)Utilities.SizeOf<VREvent_t>();

				head = Matrix.Identity;

				// Initialize Audio
				audio = new XAudio2();
				var voice = new MasteringVoice(audio);
				audio3d = new X3DAudio(Speakers.Stereo);

				foreach (var controller in controllers)
				{
					controllerEmitters[controller] = new Emitter
					{
						ChannelCount = 1,
						CurveDistanceScaler = 0.15f,
						OrientFront = Vector3.ForwardLH,
						OrientTop = Vector3.Up,
						Position = new Vector3(0, 0, 1000),
						//Velocity = Vector3.Zero
					};
				}

				listener = new Listener
				{
					OrientFront = Vector3.ForwardLH,
					OrientTop = Vector3.Up,
					Position = new Vector3(0, 0, 1000)
				};

				var audioFormat = new WaveFormat(44100, 32, 1);
				//var audioSource = new SourceVoice(audio, audioFormat);
				var audioBufferSize = audioFormat.ConvertLatencyToByteSize(1000);
				var audioStream = new DataStream(audioBufferSize, true, true);
				var audioSamples = audioBufferSize / audioFormat.BlockAlign;

				var random = new Random();

				for (var sample = 0; sample < audioSamples; sample++)
					audioStream.Write((float)random.NextFloat(-1, 1));

				audioStream.Position = 0;

				var audioBuffer = new AudioBuffer
				{
					Stream = audioStream,
					AudioBytes = audioBufferSize,
					LoopCount = 255
				};

				var audioSettings = new DspSettings(1, 2);

				foreach (var controller in controllers)
				{
					var audioSource = new SourceVoice(audio, audioFormat);
					
					audioSource.SubmitSourceBuffer(audioBuffer, null);

					audio3d.Calculate(listener, controllerEmitters[controller], CalculateFlags.Matrix, audioSettings);

					audioSource.SetOutputMatrix(1, 2, audioSettings.MatrixCoefficients);

					audioSource.Start();

					controllerVoices[controller] = audioSource;
				}

				RenderLoop.Run(form, () =>
				{
					while (system.PollNextEvent(ref vrEvent, eventSize))
					{
						switch ((EVREventType)vrEvent.eventType)
						{
							case EVREventType.VREvent_TrackedDeviceActivated:
								var controller = vrEvent.trackedDeviceIndex;

								controllers.Add(controller);

								var modelName = new StringBuilder(255, 255);
								var propertyError = ETrackedPropertyError.TrackedProp_Success;

								var length = system.GetStringTrackedDeviceProperty(controller, ETrackedDeviceProperty.Prop_RenderModelName_String, modelName, 255, ref propertyError);

								if (propertyError == ETrackedPropertyError.TrackedProp_Success)
								{
									var modelName2 = modelName.ToString();

									while (true)
									{
										var pointer = IntPtr.Zero;
										var modelError = EVRRenderModelError.None;

										modelError = OpenVR.RenderModels.LoadRenderModel_Async(modelName2, ref pointer);

										if (modelError == EVRRenderModelError.Loading)
											continue;

										if (modelError == EVRRenderModelError.None)
										{
											var renderModel = System.Runtime.InteropServices.Marshal.PtrToStructure<RenderModel_t>(pointer);

											controllerModels[controller] = renderModel;

											// Load Controller Model
											var model = controllerModels[controller];

											controllerVertexBuffers[controller] = new SharpDX.Direct3D11.Buffer(device, model.rVertexData, new BufferDescription
											{
												BindFlags = BindFlags.VertexBuffer,
												SizeInBytes = (int)model.unVertexCount * 32
											});

											controllerVertexBufferBindings[controller] = new VertexBufferBinding(controllerVertexBuffers[controller], 32, 0);

											controllerIndexBuffers[controller] = new SharpDX.Direct3D11.Buffer(device, model.rIndexData, new BufferDescription
											{
												BindFlags = BindFlags.IndexBuffer,
												SizeInBytes = (int)model.unTriangleCount * 3 * 2
											});

											break;
										}
									}

									while (true)
									{
										var pointer = IntPtr.Zero;
										var textureError = EVRRenderModelError.None;

										textureError = OpenVR.RenderModels.LoadTexture_Async(controllerModels[controller].diffuseTextureId, ref pointer);

										if (textureError == EVRRenderModelError.Loading)
											continue;

										if (textureError == EVRRenderModelError.None)
										{
											var textureMap = System.Runtime.InteropServices.Marshal.PtrToStructure<RenderModel_TextureMap_t>(pointer);

											controllerTextures[controller] = textureMap;

											using (var texture2d = new Texture2D(device, new Texture2DDescription
											{
												ArraySize = 1,
												BindFlags = BindFlags.ShaderResource,
												Format = Format.R8G8B8A8_UNorm,
												Width = textureMap.unWidth,
												Height = textureMap.unHeight,
												MipLevels = 1,
												SampleDescription = new SampleDescription(1, 0)
											}, new DataRectangle(textureMap.rubTextureMapData, textureMap.unWidth * 4)))
												controllerTextureViews[controller] = new ShaderResourceView(device, texture2d);

											break;
										}
									}

									controllerEmitters[controller] = new Emitter
									{
										ChannelCount = 1,
										CurveDistanceScaler = 0.15f,
										OrientFront = Vector3.ForwardLH,
										OrientTop = Vector3.Up,
										Position = new Vector3(0, 0, 1000),
										//Velocity = Vector3.Zero
									};

									var audioSource = new SourceVoice(audio, audioFormat);

									audioSource.SubmitSourceBuffer(audioBuffer, null);

									audio3d.Calculate(listener, controllerEmitters[controller], CalculateFlags.Matrix, audioSettings);

									audioSource.SetOutputMatrix(1, 2, audioSettings.MatrixCoefficients);

									audioSource.Start();

									controllerVoices[controller] = audioSource;
								}
								break;

							case EVREventType.VREvent_TrackedDeviceDeactivated:
								controllers.RemoveAll(c => c == vrEvent.trackedDeviceIndex);
								break;

							default:
								System.Diagnostics.Debug.WriteLine((EVREventType)vrEvent.eventType);
								break;
						}
					}

					if (form.ClientSize != windowSize)
					{
						Utilities.Dispose(ref backBufferView);

						if (form.ClientSize.Width != 0 && form.ClientSize.Height != 0)
						{
							swapChain.ResizeBuffers(1, form.ClientSize.Width, form.ClientSize.Height, Format.B8G8R8A8_UNorm, SwapChainFlags.None);

							using (var backBuffer = swapChain.GetBackBuffer<Texture2D>(0))
								backBufferView = new RenderTargetView(device, backBuffer);
						}

						windowSize = form.ClientSize;
					}

					// Update Device Tracking
					compositor.WaitGetPoses(currentPoses, nextPoses);

					if (currentPoses[headset].bPoseIsValid)
					{
						Convert(ref currentPoses[headset].mDeviceToAbsoluteTracking, ref head);
						
						// Update Audio Listener
						listener.Position = head.TranslationVector * new Vector3(1, 1, -1);
						listener.OrientFront = head.Forward * new Vector3(1, 1, -1);
						listener.OrientTop = head.Up * new Vector3(1, 1, -1);
					}

					foreach (var controller in controllers)
					{
						var controllerMatrix = Matrix.Identity;

						Convert(ref currentPoses[controller].mDeviceToAbsoluteTracking, ref controllerMatrix);

						var position = controllerMatrix.TranslationVector * new Vector3(1, 1, -1);

						controllerEmitters[controller].Position = position;

						audio3d.Calculate(listener, controllerEmitters[controller], CalculateFlags.Matrix, audioSettings);

						controllerVoices[controller].SetOutputMatrix(1, 2, audioSettings.MatrixCoefficients);
					}

					// Render Left Eye
					context.Rasterizer.SetViewport(0, 0, headsetSize.Width, headsetSize.Height);
					context.OutputMerger.SetTargets(eyeDepthView, leftEyeTextureView);
					context.OutputMerger.SetDepthStencilState(depthStencilState);

					context.ClearRenderTargetView(leftEyeTextureView, backgroundColor);
					context.ClearDepthStencilView(eyeDepthView, DepthStencilClearFlags.Depth, 1.0f, 0);

					Shaders.Apply(context);

					context.Rasterizer.State = rasterizerState;

					context.OutputMerger.SetBlendState(blendState);
					context.OutputMerger.SetDepthStencilState(depthStencilState);

					context.PixelShader.SetSampler(0, samplerState);

					var ratio = (float)headsetSize.Width / (float)headsetSize.Height;

					var projection = leftEyeProjection;
					var view = Matrix.Invert(leftEyeView * head);
					var world = Matrix.Scaling(0.5f) * Matrix.Translation(0, 1.0f, 0);

					worldViewProjection = world * view * projection;

					context.UpdateSubresource(ref worldViewProjection, worldViewProjectionBuffer);

					context.VertexShader.SetConstantBuffer(0, worldViewProjectionBuffer);

					//Shapes.Cube.Begin(context);
					//Shapes.Cube.Draw(context);

					Shapes.Sphere.Begin(context);
					Shapes.Sphere.Draw(context);

					// Draw Controllers
					context.InputAssembler.InputLayout = controllerLayout;
					context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;

					context.VertexShader.Set(controllerVertexShader);
					context.PixelShader.Set(controllerPixelShader);
					context.GeometryShader.Set(null);
					context.DomainShader.Set(null);
					context.HullShader.Set(null);

					context.PixelShader.SetSampler(0, samplerState);

					foreach (var controller in controllers)
					{
						context.InputAssembler.SetVertexBuffers(0, controllerVertexBufferBindings[controller]);
						context.InputAssembler.SetIndexBuffer(controllerIndexBuffers[controller], Format.R16_UInt, 0);

						context.PixelShader.SetShaderResource(0, controllerTextureViews[controller]);

						Convert(ref currentPoses[controller].mDeviceToAbsoluteTracking, ref world);

						worldViewProjection = world * view * projection;

						context.UpdateSubresource(ref worldViewProjection, worldViewProjectionBuffer);

						context.VertexShader.SetConstantBuffer(0, worldViewProjectionBuffer);

						context.DrawIndexed((int)controllerModels[controller].unTriangleCount * 3 * 4, 0, 0);
					}

					var texture = new Texture_t
					{
						eType = ETextureType.DirectX,
						eColorSpace = EColorSpace.Gamma,
						handle = leftEyeTextureView.Resource.NativePointer
					};

					var bounds = new VRTextureBounds_t
					{
						uMin = 0.0f,
						uMax = 1.0f,
						vMin = 0.0f,
						vMax = 1.0f,
					};

					var submitError = compositor.Submit(EVREye.Eye_Left, ref texture, ref bounds, EVRSubmitFlags.Submit_Default);

					if (submitError != EVRCompositorError.None)
						System.Diagnostics.Debug.WriteLine(submitError);

					// Render Right Eye
					context.Rasterizer.SetViewport(0, 0, headsetSize.Width, headsetSize.Height);
					context.OutputMerger.SetTargets(eyeDepthView, rightEyeTextureView);
					context.OutputMerger.SetDepthStencilState(depthStencilState);

					context.ClearRenderTargetView(rightEyeTextureView, backgroundColor);
					context.ClearDepthStencilView(eyeDepthView, DepthStencilClearFlags.Depth, 1.0f, 0);

					Shaders.Apply(context);

					context.Rasterizer.State = rasterizerState;

					context.OutputMerger.SetBlendState(blendState);
					context.OutputMerger.SetDepthStencilState(depthStencilState);

					context.PixelShader.SetSampler(0, samplerState);

					projection = rightEyeProjection;
					view = Matrix.Invert(rightEyeView * head);
					world = Matrix.Scaling(0.5f) * Matrix.Translation(0, 1.0f, 0);

					worldViewProjection = world * view * projection;

					context.UpdateSubresource(ref worldViewProjection, worldViewProjectionBuffer);

					context.VertexShader.SetConstantBuffer(0, worldViewProjectionBuffer);

					//Shapes.Cube.Begin(context);
					//Shapes.Cube.Draw(context);

					Shapes.Sphere.Begin(context);
					Shapes.Sphere.Draw(context);

					// Draw Controllers
					context.InputAssembler.InputLayout = controllerLayout;
					context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;

					context.VertexShader.Set(controllerVertexShader);
					context.PixelShader.Set(controllerPixelShader);
					context.GeometryShader.Set(null);
					context.DomainShader.Set(null);
					context.HullShader.Set(null);

					context.PixelShader.SetSampler(0, samplerState);

					foreach (var controller in controllers)
					{
						context.InputAssembler.SetVertexBuffers(0, controllerVertexBufferBindings[controller]);
						context.InputAssembler.SetIndexBuffer(controllerIndexBuffers[controller], Format.R16_UInt, 0);

						context.PixelShader.SetShaderResource(0, controllerTextureViews[controller]);

						Convert(ref currentPoses[controller].mDeviceToAbsoluteTracking, ref world);

						worldViewProjection = world * view * projection;

						context.UpdateSubresource(ref worldViewProjection, worldViewProjectionBuffer);

						context.VertexShader.SetConstantBuffer(0, worldViewProjectionBuffer);

						context.DrawIndexed((int)controllerModels[controller].unTriangleCount * 3 * 4, 0, 0);
					}

					texture.handle = rightEyeTextureView.Resource.NativePointer;

					submitError = compositor.Submit(EVREye.Eye_Right, ref texture, ref bounds, EVRSubmitFlags.Submit_Default);

					if (submitError != EVRCompositorError.None)
						System.Diagnostics.Debug.WriteLine(submitError);

					// Render Window
					context.Rasterizer.SetViewport(0, 0, windowSize.Width, windowSize.Height);

					context.OutputMerger.SetTargets(depthStencilView, backBufferView);
					context.OutputMerger.SetDepthStencilState(depthStencilState);

					context.ClearRenderTargetView(backBufferView, backgroundColor);
					context.ClearDepthStencilView(depthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);

					Shaders.Apply(context);

					context.Rasterizer.State = rasterizerState;

					context.OutputMerger.SetBlendState(blendState);
					context.OutputMerger.SetDepthStencilState(depthStencilState);

					context.PixelShader.SetSampler(0, samplerState);

					ratio = (float)form.ClientSize.Width / (float)form.ClientSize.Height;

					projection = Matrix.PerspectiveFovRH(3.14F / 3.0F, ratio, 0.01f, 1000);
					view = Matrix.Invert(head);
					world = Matrix.Scaling(0.5f) * Matrix.Translation(0, 1.0f, 0);

					worldViewProjection = world * view * projection;

					context.UpdateSubresource(ref worldViewProjection, worldViewProjectionBuffer);

					context.VertexShader.SetConstantBuffer(0, worldViewProjectionBuffer);

					//Shapes.Cube.Begin(context);
					//Shapes.Cube.Draw(context);

					Shapes.Sphere.Begin(context);
					Shapes.Sphere.Draw(context);

					// Draw Controllers
					context.InputAssembler.InputLayout = controllerLayout;
					context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;

					context.VertexShader.Set(controllerVertexShader);
					context.PixelShader.Set(controllerPixelShader);
					context.GeometryShader.Set(null);
					context.DomainShader.Set(null);
					context.HullShader.Set(null);

					context.PixelShader.SetSampler(0, samplerState);

					foreach (var controller in controllers)
					{
						context.InputAssembler.SetVertexBuffers(0, controllerVertexBufferBindings[controller]);
						context.InputAssembler.SetIndexBuffer(controllerIndexBuffers[controller], Format.R16_UInt, 0);

						Convert(ref currentPoses[controller].mDeviceToAbsoluteTracking, ref world);

						worldViewProjection = world * view * projection;

						context.UpdateSubresource(ref worldViewProjection, worldViewProjectionBuffer);

						context.VertexShader.SetConstantBuffer(0, worldViewProjectionBuffer);

						context.DrawIndexed((int)controllerModels[controller].unTriangleCount * 3 * 4, 0, 0);
					}

					// Show Backbuffer
					swapChain.Present(0, PresentFlags.None);
				});

				audio.Dispose();
			}
		}

		private static void Convert(ref HmdMatrix34_t source, ref Matrix destination)
		{
			destination.M11 = source.m0;
			destination.M21 = source.m1;
			destination.M31 = source.m2;
			destination.M41 = source.m3;
			destination.M12 = source.m4;
			destination.M22 = source.m5;
			destination.M32 = source.m6;
			destination.M42 = source.m7;
			destination.M13 = source.m8;
			destination.M23 = source.m9;
			destination.M33 = source.m10;
			destination.M43 = source.m11;
			//destination.M14 = 0.0f;
			//destination.M24 = 0.0f;
			//destination.M34 = 0.0f;
			//destination.M44 = 1.0f;
		}

		private static Matrix Convert(HmdMatrix34_t source)
		{
			var destination = new Matrix();

			destination.M11 = source.m0;
			destination.M21 = source.m1;
			destination.M31 = source.m2;
			destination.M41 = source.m3;
			destination.M12 = source.m4;
			destination.M22 = source.m5;
			destination.M32 = source.m6;
			destination.M42 = source.m7;
			destination.M13 = source.m8;
			destination.M23 = source.m9;
			destination.M33 = source.m10;
			destination.M43 = source.m11;
			destination.M14 = 0.0f;
			destination.M24 = 0.0f;
			destination.M34 = 0.0f;
			destination.M44 = 1.0f;

			return destination;
		}

		private static Matrix Convert(HmdMatrix44_t source)
		{
			var destination = new Matrix();

			destination.M11 = source.m0;
			destination.M21 = source.m1;
			destination.M31 = source.m2;
			destination.M41 = source.m3;
			destination.M12 = source.m4;
			destination.M22 = source.m5;
			destination.M32 = source.m6;
			destination.M42 = source.m7;
			destination.M13 = source.m8;
			destination.M23 = source.m9;
			destination.M33 = source.m10;
			destination.M43 = source.m11;
			destination.M14 = source.m12;
			destination.M24 = source.m13;
			destination.M34 = source.m14;
			destination.M44 = source.m15;

			return destination;
		}
	}

	public struct ColorVertex
	{
		public Vector3 Position;
		public Vector4 Color;

		public ColorVertex(Vector3 position, Vector4 color)
		{
			Position = position;
			Color = color;
		}
	}
}