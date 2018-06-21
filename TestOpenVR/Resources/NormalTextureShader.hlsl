cbuffer data : register(b0)
{
	float4x4 worldViewProjection;
};

Texture2D textureMap;
SamplerState textureSampler
{
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = Wrap;
	AddressV = Wrap;
};

struct VS_IN
{
	float4 position : POSITION;
	float4 normal : NORMAL;
	float2 texCoord : TEXCOORD;
};

struct PS_IN
{
	float4 position : SV_POSITION;
	float4 color : COLOR;
	float2 texCoord : TEXCOORD;
};

PS_IN VS(VS_IN input)
{
	PS_IN output = (PS_IN)0;

	output.position = mul(worldViewProjection, input.position);

	float3 light = { 0, 1, 0 };

	float4 normal = mul(worldViewProjection, input.normal);

	output.color.rgb = dot(normal, light);
	output.color.a = 1;

	output.texCoord = input.texCoord;

	return output;
}

float4 PS(PS_IN input) : SV_Target
{
	return input.color + textureMap.Sample(textureSampler, input.texCoord);
}