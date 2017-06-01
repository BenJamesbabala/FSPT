#version 300 es
precision highp float;
const int sphereCount = 14;
const int NUM_BOUNCES = 7;
const float max_t = 100000.0;
const float n1 = 1.0;
const float n2 = 1.458;
const float sr = n1/n2;
const float r0 = ((n1 - n2)/(n1 + n2))*((n1 - n2)/(n1 + n2));
const float M_PI = 3.1415926535897932384626433832795;
const float epsilon = 0.00001; //not really epsilon
const float gamma = 1.0/2.2;

const uint FROM_PARENT = 0;
const uint FROM_SIBLING = 1;
const uint FROM_CHILD = 2;

in vec2 coords;
out vec4 fragColor;

uniform int tick;
uniform vec3 eye;
uniform sampler2D fbTex;
uniform sampler2D triTex;
uniform sampler2D bvhTex;

struct Triangle {
  vec3 v1;
  vec3 v2;
  vec3 v3;
};

struct Ray {
  vec3 origin;
  vec3 dir;
};

struct Node {
  float parent;
  float sibling;
  float split;
  float left;
  float right;
  float triangles;
  vec3 boxMin;
  vec3 boxMax;
};

struct Hit {
  Ray ray;
  vec3 emmittance;
  vec3 reflectance;
  int index;
};

float rand(vec2 co){
  float a = 12.9898;
  float b = 78.233;
  float c = 43758.5453;
  float dt= dot(co ,vec2(a,b) + float(tick) * 0.0194161103873);
  float sn= mod(dt,M_PI);
  return fract(sin(sn) * c);
}

float getAngle(vec3 a){
  vec3 b = vec3(0.0,0.0,1.0);
  return atan(length(cross(a,b)),a.z);
}

mat3 rotationMatrix(vec3 axis, float angle){
  axis = normalize(axis);
  float s = sin(angle);
  float c = cos(angle);
  float oc = 1.0 - c;
  return mat3(oc * axis.x * axis.x + c,    oc * axis.x * axis.y - axis.z * s, oc * axis.z * axis.x + axis.y * s,
        oc * axis.x * axis.y + axis.z * s, oc * axis.y * axis.y + c,          oc * axis.y * axis.z - axis.x * s,
        oc * axis.z * axis.x - axis.y * s, oc * axis.y * axis.z + axis.x * s, oc * axis.z * axis.z + c);
}

ivec2 indexToCoords(sampler2D tex, float index, uint perElement){
    ivec2 dims = textureSize(tex, 0);
    return ivec2(mod(index * perElement, float(dims.x)), floor((index * perElement)/ float(dims.x)));
}

vec3 randomVec(vec3 normal, vec3 origin, float exp){
  float r2 = rand(origin.xz);
  float r1 = rand(origin.xy)-epsilon;
  float r = pow(r1,exp);
  float theta = 2.0 * M_PI * r2;
  float x = r * cos(theta);
  float y = r * sin(theta);
  vec3 rv = vec3(x, y, sqrt(1.0 - r*r));
  float phi = getAngle(normal);
  return rotationMatrix(cross(normal,vec3(0.0,0.0,1.0)),phi) * rv;
}

float rayTriangleIntersect(Ray ray, Triangle tri){
  float epsilon = 0.000000000001;
  vec3 e1 = tri.v2 - tri.v1;
  vec3 e2 = tri.v3 - tri.v1;
  vec3 p = cross(ray.dir, e2);
  float det = dot(e1, p);
  if(det > -epsilon && det < epsilon){return max_t;}
  float invDet = 1.0 / det;
  vec3 t = ray.origin - tri.v1;
  float u = dot(t, p) * invDet;
  if(u < 0.0 || u > 1.0){return max_t;}
  vec3 q = cross(t, e1);
  float v = dot(ray.dir, q) * invDet;
  if(v < 0.0 || u + v > 1.0){return max_t;}
  float dist = dot(e2, q) * invDet;
  if(dist > epsilon){
    return dist;
  }
  return max_t;
}

vec2 getDOF(){
  float theta = rand(coords) * M_PI * 2.0;
  float sqrt_r = sqrt(rand(coords.yx));
  return vec2(sqrt_r * cos(theta), sqrt_r * sin(theta));
}

// Hit getSpecular(int i, float t, Ray ray, Sphere s){
//   Hit result;
//   result.ray.origin = ray.dir*t + ray.origin;
//   vec3 normal = normalize(result.ray.origin - s.origin);
//   result.index = i;
//   normal = randomVec(normal, result.ray.origin, s.material.y);
//   result.ray.dir = reflect(ray.dir,normal);
//   result.reflectance = vec3(1);
//   if(dot(result.ray.dir,normal) < 0.0){
//     result.ray.dir = -result.ray.dir;
//   }
//   return result;
// }
//
// Hit getLambertian(int i, float t, Ray ray, Sphere s){
//   Hit result;
//   result.ray.origin = ray.dir*t + ray.origin;
//   vec3 normal = normalize(result.ray.origin - s.origin);
//   result.emmittance = s.attrs.z * s.color;
//   result.index = i;
//   result.ray.dir = randomVec(normal, result.ray.origin, 0.5);
//   result.reflectance = s.color;
//   return result;
// }
//
// Hit getTransmissive(int i, float t, Ray ray, Sphere s){
//   Hit result;
//   result.ray.origin = ray.dir*t + ray.origin;
//   vec3 normal = normalize(result.ray.origin - s.origin);
//   result.index = i;
//   float dh = 1.0 - dot(-ray.dir,normal);
//   float re = r0 + (1.0 - r0)*dh*dh*dh*dh*dh;
//   if(rand(result.ray.origin.xy) < re){
//     result.ray.dir = reflect(ray.dir, normal);
//   }else{
//     float c = dot(ray.dir,-normal);
//     vec3 ref = normalize(sr*ray.dir + (sr*c - sqrt(1.0 - sr*sr*(1.0 - c*c)))*normal);
//     result.ray.origin = ref*dot(ref,-normal)*s.attrs.r*2.0 + result.ray.origin;
//     result.ray.dir = reflect(-ray.dir,ref);
//   }
//   result.reflectance = vec3(1);
//   return result;
// }
//
// Hit getCollision(Ray ray, int current){
//   float t = max_t;
//   int mat = -1;
//   Hit result;
//   for(int i=0; i<sphereCount; i++){
//     Sphere s = Sphere(spherePositions[i],sphereAttrs[i],sphereColors[i],sphereMats[i]);
//     float nt = checkSphereCollision(ray,s);
//     if(nt < t && nt > 0.0 && current != i){
//       t = nt;
//       mat = int(s.material.z);
//       if( int(s.material.z) == 0 ){ //diffuse
//         result = getLambertian(i, t, ray,s);
//       } else if( int(s.material.z) == 1 ){ //specular
//         result = getSpecular(i, t, ray,s);
//       } else if( int(s.material.z) == 2 ){ //transmissive
//         result = getTransmissive(i, t, ray, s);
//       }
//     }
//   }
//   return result;
// }

Triangle createTriangle(float index){
    ivec2 triCoords = indexToCoords(triTex, index, 3);
    return Triangle(
      texelFetch(triTex, indexToCoords(triTex, index, 3), 0),
      texelFetch(triTex, indexToCoords(triTex, index + 1, 3), 0),
      texelFetch(triTex, indexToCoords(triTex, index + 2, 3)iCoords, 0)
    );
}

Node createNode(float index){
  vec2 nodeCoords = indexToCoords(bvhTex, index, 4);
  vec3 first = texelFetch(bvhTex, nodeCoords, 0);
  vec3 second = texelFetch(bvhTex, nodeCoords + vec2(1,0), 0);
  vec3 bbMin = texelFetch(bvhTex, nodeCoords + vec2(2,0), 0);
  vec3 bbMax = texelFetch(bvhTex, nodeCoords + vec2(3,0), 0);
  return Node(first.x, first.y, first.z, second.x, second.y, second.z, bbMin, bbMax);
}

Node nearChild(Node node, Ray ray){
  uint axis = uint(node.split);
  float index = ray.dir[axis] > 0 ? node.left : node.right;
  return createNode(index);
}

bool rayBoxIntersect(Node node, Ray ray){
  vec3 inverse = 1.0 / ray.dir;
  vec3 t1 = (node.boxMin - ray.origin) * inverse;
  vec3 t2 = (node.boxMax - ray.origin) * inverse;
  vec3 maxT = min(vec3(1000000.0), max(t1, t2));
  vec3 minT = max(vec3(-1000000.0), min(t1, t2));

  return max(max(maxT.x, maxT.y),maxT.z) >= min(min(minT.x, minT.y),minT.z);
}

Hit traverseTree(Ray ray){

}

void main(void) {
  Node root = createNode(0.0);
  vec3 origin = vec3(coords.x, coords.y, 0);
  vec3 dof = vec3(getDOF(), 0.0)/ vec2(textureSize(fbTex, 0)).x;
  Ray ray = Ray(origin, normalize(origin - eye) + dof );
  float dist = max_t;
  ivec2 bvhc = ivec2(texelFetch(bvhTex,ivec2(0,0),0).rg);
  for(int i=0; i< 1; i++) {
    vec3 v2 = texelFetch(triTex, bvhc + ivec2(0,i), 0).rgb;
    vec3 v1 = texelFetch(triTex, bvhc + ivec2(1,i), 0).rgb;
    vec3 v3 = texelFetch(triTex, bvhc + ivec2(2,i), 0).rgb;
    Triangle tri = Triangle(v1,v2,v3);
    dist = min(dist, rayTriangleIntersect(ray, tri));
  }
  vec3 tcolor = texelFetch(fbTex, ivec2(gl_FragCoord), 0).rgb;
  vec3 color = dist < max_t ? vec3(0,1,0) : vec3(0);
  fragColor = clamp(vec4((color + (tcolor * float(tick)))/(float(tick)+1.0),1.0),vec4(0), vec4(1));
}

// void main(void) {
//   vec2 size = vec2(textureSize(fbTex, 0));
//   vec3 tcolor = texelFetch(fbTex, ivec2(gl_FragCoord), 0).rgb;
//   vec3 dof = 15.0 * vec3(getDOF(), 0.0) / size.x;
//   vec3 origin = vec3(coords.x, coords.y, 0) + dof * 1.5;
//   //No recursion in GLSL
//   vec3 emmittance[NUM_BOUNCES];
//   vec3 reflectance[NUM_BOUNCES];
//   Hit current = Hit(Ray(origin, normalize(origin - (eye + 4.0 * dof))), vec3(0), vec3(0), -1);
//   for(int i=0; i < NUM_BOUNCES; i++){
//     current = getCollision(current.ray, current.index);
//     emmittance[i] = current.emmittance;
//     reflectance[i] = current.reflectance;
//   }
//   vec3 color = vec3(0);
//   for(int i=NUM_BOUNCES-1; i>=0; i--){
//     color = reflectance[i]*color + emmittance[i];
//   }
//   color = pow(color,vec3(gamma));
//   fragColor = clamp(vec4((color + (tcolor * float(tick)))/(float(tick)+1.0),1.0),vec4(0), vec4(1));
// }
