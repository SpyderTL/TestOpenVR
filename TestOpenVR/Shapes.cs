using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;

namespace TestOpenVR
{
	public static class Shapes
	{
		private const int stride = 8;

		public static class Cube
		{
			private static VertexBufferBinding vertexBufferBinding;
			private static SharpDX.Direct3D11.Buffer vertexBuffer;
			private static SharpDX.Direct3D11.Buffer indexBuffer;

			private static float[] vertices = new float[]
			{
				//TOP
				-0.5f, 0.5f, 0.5f, 0, 1, 0, 0, 0,
				0.5f, 0.5f, 0.5f, 0, 1, 0, 1, 0,
				0.5f, 0.5f,-0.5f, 0, 1, 0, 1, 1,
				-0.5f, 0.5f,-0.5f, 0, 1, 0, 0, 1,
				//BOTTOM
				-0.5f,-0.5f, 0.5f, 0, -1, 0, 0, 0,
				0.5f,-0.5f, 0.5f, 0, -1, 0, 1, 0,
				0.5f,-0.5f,-0.5f, 0, -1, 0, 1, 1,
				-0.5f,-0.5f,-0.5f, 0, -1, 0, 0, 1,
				//LEFT
				-0.5f,-0.5f, 0.5f, -1, 0, 0, 0, 0,
				-0.5f, 0.5f, 0.5f, -1, 0, 0, 1, 0,
				-0.5f, 0.5f,-0.5f, -1, 0, 0, 1, 1,
				-0.5f,-0.5f,-0.5f, -1, 0, 0, 0, 1,
				//RIGHT
				0.5f,-0.5f, 0.5f, 1, 0, 0, 0, 0,
				0.5f, 0.5f, 0.5f, 1, 0, 0, 1, 0,
				0.5f, 0.5f,-0.5f, 1, 0, 0, 1, 1,
				0.5f,-0.5f,-0.5f, 1, 0, 0, 0, 1,
				//FRONT
				-0.5f, 0.5f, 0.5f, 0, 0, 1, 0, 0,
				0.5f, 0.5f, 0.5f, 0, 0, 1, 1, 0,
				0.5f,-0.5f, 0.5f, 0, 0, 1, 1, 1,
				-0.5f,-0.5f, 0.5f, 0, 0, 1, 0, 1,
				//BACK
				-0.5f, 0.5f,-0.5f, 0, 0, -1, 0, 0,
				0.5f, 0.5f,-0.5f, 0, 0, -1, 1, 0,
				0.5f,-0.5f,-0.5f, 0, 0, -1, 1, 1,
				-0.5f,-0.5f,-0.5f, 0, 0, -1, 0, 1
			};

			private static short[] indices = new short[]
			{
			0,1,2,0,2,3,
			4,6,5,4,7,6,
			8,9,10,8,10,11,
			12,14,13,12,15,14,
			16,18,17,16,19,18,
			20,21,22,20,22,23
			};

			public static void Load(Device device)
			{
				// Load Cube
				var handle = System.Runtime.InteropServices.GCHandle.Alloc(vertices, System.Runtime.InteropServices.GCHandleType.Pinned);

				vertexBuffer = new SharpDX.Direct3D11.Buffer(device, handle.AddrOfPinnedObject(), new BufferDescription
				{
					BindFlags = BindFlags.VertexBuffer,
					SizeInBytes = sizeof(float) * vertices.Length
				});

				handle.Free();

				handle = System.Runtime.InteropServices.GCHandle.Alloc(indices, System.Runtime.InteropServices.GCHandleType.Pinned);

				indexBuffer = new SharpDX.Direct3D11.Buffer(device, handle.AddrOfPinnedObject(), new BufferDescription
				{
					BindFlags = BindFlags.IndexBuffer,
					SizeInBytes = sizeof(ushort) * indices.Length
				});

				handle.Free();

				vertexBufferBinding = new VertexBufferBinding(vertexBuffer, sizeof(float) * stride, 0);
			}

			public static void Begin(DeviceContext context)
			{
				context.InputAssembler.SetVertexBuffers(0, vertexBufferBinding);
				context.InputAssembler.SetIndexBuffer(indexBuffer, SharpDX.DXGI.Format.R16_UInt, 0);
				context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
			}

			public static void Draw(DeviceContext context)
			{
				context.DrawIndexed(indices.Length, 0, 0);
			}
		}

		public static class Sphere
		{
			private static SharpDX.Direct3D11.Buffer vertexBuffer;
			private static SharpDX.Direct3D11.Buffer indexBuffer;
			private static VertexBufferBinding vertexBufferBinding;
			private static ushort[] indices;

			public static void Load(Device device)
			{
				// Load Sphere
				var horizontalSlices = 64;
				var verticalSlices = 64;

				var sphereVertexData = new float[((horizontalSlices * verticalSlices) + 2) * stride];

				for (var horizontalSlice = 0; horizontalSlice < horizontalSlices; horizontalSlice++)
				{
					var y = (float)Math.Cos((MathUtil.Pi / (horizontalSlices + 1)) * (horizontalSlice + 1)) * 0.5f;

					for (var verticalSlice = 0; verticalSlice < verticalSlices; verticalSlice++)
					{
						var x = (float)(Math.Sin((MathUtil.TwoPi / verticalSlices) * verticalSlice) * Math.Sin((MathUtil.Pi / (horizontalSlices + 1)) * (horizontalSlice + 1))) * 0.5f;
						var z = (float)(Math.Cos((MathUtil.TwoPi / verticalSlices) * verticalSlice) * Math.Sin((MathUtil.Pi / (horizontalSlices + 1)) * (horizontalSlice + 1))) * 0.5f;

						sphereVertexData[(((horizontalSlice * verticalSlices) + verticalSlice) * stride) + 0] = x;
						sphereVertexData[(((horizontalSlice * verticalSlices) + verticalSlice) * stride) + 1] = y;
						sphereVertexData[(((horizontalSlice * verticalSlices) + verticalSlice) * stride) + 2] = z;

						var normal = new Vector3(x, y, z);

						normal.Normalize();

						sphereVertexData[(((horizontalSlice * verticalSlices) + verticalSlice) * stride) + 3] = normal.X;
						sphereVertexData[(((horizontalSlice * verticalSlices) + verticalSlice) * stride) + 4] = normal.Y;
						sphereVertexData[(((horizontalSlice * verticalSlices) + verticalSlice) * stride) + 5] = normal.Z;
					}
				}

				sphereVertexData[(((horizontalSlices * verticalSlices) + 0) * stride) + 1] = 0.5f;
				sphereVertexData[(((horizontalSlices * verticalSlices) + 0) * stride) + 4] = 1.0f;
				sphereVertexData[(((horizontalSlices * verticalSlices) + 1) * stride) + 1] = -0.5f;
				sphereVertexData[(((horizontalSlices * verticalSlices) + 1) * stride) + 4] = -1.0f;

				indices = new ushort[(((horizontalSlices - 1) * (verticalSlices * 6)) + (verticalSlices * 3 * 2)) * stride];

				for (var horizontalSlice = 0; horizontalSlice < horizontalSlices - 1; horizontalSlice++)
				{
					for (var verticalSlice = 0; verticalSlice < verticalSlices - 1; verticalSlice++)
					{
						indices[(((horizontalSlice * verticalSlices) + verticalSlice) * 6) + 0] = (ushort)(((horizontalSlice + 0) * verticalSlices) + (verticalSlice + 0));
						indices[(((horizontalSlice * verticalSlices) + verticalSlice) * 6) + 1] = (ushort)(((horizontalSlice + 1) * verticalSlices) + (verticalSlice + 1));
						indices[(((horizontalSlice * verticalSlices) + verticalSlice) * 6) + 2] = (ushort)(((horizontalSlice + 0) * verticalSlices) + (verticalSlice + 1));
						indices[(((horizontalSlice * verticalSlices) + verticalSlice) * 6) + 3] = (ushort)(((horizontalSlice + 0) * verticalSlices) + (verticalSlice + 0));
						indices[(((horizontalSlice * verticalSlices) + verticalSlice) * 6) + 4] = (ushort)(((horizontalSlice + 1) * verticalSlices) + (verticalSlice + 0));
						indices[(((horizontalSlice * verticalSlices) + verticalSlice) * 6) + 5] = (ushort)(((horizontalSlice + 1) * verticalSlices) + (verticalSlice + 1));
					}

					indices[(((horizontalSlice * verticalSlices) + verticalSlices - 1) * 6) + 0] = (ushort)(((horizontalSlice + 0) * verticalSlices) + ((verticalSlices - 1) * 1));
					indices[(((horizontalSlice * verticalSlices) + verticalSlices - 1) * 6) + 1] = (ushort)(((horizontalSlice + 1) * verticalSlices) + ((verticalSlices - 1) * 0));
					indices[(((horizontalSlice * verticalSlices) + verticalSlices - 1) * 6) + 2] = (ushort)(((horizontalSlice + 0) * verticalSlices) + ((verticalSlices - 1) * 0));
					indices[(((horizontalSlice * verticalSlices) + verticalSlices - 1) * 6) + 3] = (ushort)(((horizontalSlice + 0) * verticalSlices) + ((verticalSlices - 1) * 1));
					indices[(((horizontalSlice * verticalSlices) + verticalSlices - 1) * 6) + 4] = (ushort)(((horizontalSlice + 1) * verticalSlices) + ((verticalSlices - 1) * 1));
					indices[(((horizontalSlice * verticalSlices) + verticalSlices - 1) * 6) + 5] = (ushort)(((horizontalSlice + 1) * verticalSlices) + ((verticalSlices - 1) * 0));
				}

				for (var verticalSlice = 0; verticalSlice < verticalSlices - 1; verticalSlice++)
				{
					indices[((((horizontalSlices - 1) * verticalSlices) * 6) + (verticalSlice * 3)) + 0] = (ushort)((((1 - 1) + 0) * verticalSlices) + (verticalSlice + 0));
					indices[((((horizontalSlices - 1) * verticalSlices) * 6) + (verticalSlice * 3)) + 1] = (ushort)((((1 - 1) + 0) * verticalSlices) + (verticalSlice + 1));
					indices[((((horizontalSlices - 1) * verticalSlices) * 6) + (verticalSlice * 3)) + 2] = (ushort)((horizontalSlices * verticalSlices) + 0);
				}

				indices[((((horizontalSlices - 1) * verticalSlices) * 6) + ((verticalSlices - 1) * 3)) + 0] = (ushort)((((1 - 1) + 0) * verticalSlices) + ((verticalSlices - 1) * 1));
				indices[((((horizontalSlices - 1) * verticalSlices) * 6) + ((verticalSlices - 1) * 3)) + 1] = (ushort)((((1 - 1) + 0) * verticalSlices) + ((verticalSlices - 1) * 0));
				indices[((((horizontalSlices - 1) * verticalSlices) * 6) + ((verticalSlices - 1) * 3)) + 2] = (ushort)((horizontalSlices * verticalSlices) + 0);

				for (var verticalSlice = 0; verticalSlice < verticalSlices - 1; verticalSlice++)
				{
					indices[((((horizontalSlices - 1) * verticalSlices) * 6) + ((verticalSlices + verticalSlice) * 3)) + 0] = (ushort)((((horizontalSlices - 1) + 0) * verticalSlices) + (verticalSlice + 1));
					indices[((((horizontalSlices - 1) * verticalSlices) * 6) + ((verticalSlices + verticalSlice) * 3)) + 1] = (ushort)((((horizontalSlices - 1) + 0) * verticalSlices) + (verticalSlice + 0));
					indices[((((horizontalSlices - 1) * verticalSlices) * 6) + ((verticalSlices + verticalSlice) * 3)) + 2] = (ushort)((horizontalSlices * verticalSlices) + 1);
				}

				indices[((((horizontalSlices - 1) * verticalSlices) * 6) + (((verticalSlices + verticalSlices) - 1) * 3)) + 0] = (ushort)((((horizontalSlices - 1) + 0) * verticalSlices) + ((verticalSlices - 1) * 0));
				indices[((((horizontalSlices - 1) * verticalSlices) * 6) + (((verticalSlices + verticalSlices) - 1) * 3)) + 1] = (ushort)((((horizontalSlices - 1) + 0) * verticalSlices) + ((verticalSlices - 1) * 1));
				indices[((((horizontalSlices - 1) * verticalSlices) * 6) + (((verticalSlices + verticalSlices) - 1) * 3)) + 2] = (ushort)((horizontalSlices * verticalSlices) + 1);

				var handle = System.Runtime.InteropServices.GCHandle.Alloc(sphereVertexData, System.Runtime.InteropServices.GCHandleType.Pinned);

				vertexBuffer = new SharpDX.Direct3D11.Buffer(device, handle.AddrOfPinnedObject(), new BufferDescription
				{
					BindFlags = BindFlags.VertexBuffer,
					SizeInBytes = sizeof(float) * sphereVertexData.Length
				});

				handle.Free();

				handle = System.Runtime.InteropServices.GCHandle.Alloc(indices, System.Runtime.InteropServices.GCHandleType.Pinned);

				indexBuffer = new SharpDX.Direct3D11.Buffer(device, handle.AddrOfPinnedObject(), new BufferDescription
				{
					BindFlags = BindFlags.IndexBuffer,
					SizeInBytes = sizeof(ushort) * indices.Length
				});

				handle.Free();

				vertexBufferBinding = new VertexBufferBinding(vertexBuffer, sizeof(float) * stride, 0);
			}

			public static void Begin(DeviceContext context)
			{
				context.InputAssembler.SetVertexBuffers(0, vertexBufferBinding);
				context.InputAssembler.SetIndexBuffer(indexBuffer, SharpDX.DXGI.Format.R16_UInt, 0);
				context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
			}

			public static void Draw(DeviceContext context)
			{
				context.DrawIndexed(indices.Length, 0, 0);
			}
		}
	}
}
