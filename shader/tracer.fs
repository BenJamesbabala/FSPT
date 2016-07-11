precision highp float;
const int sphereCount = 14;
const float max_t = 100000.0;
const float n1 = 1.0;
const float n2 = 1.458;
const float sr = n1/n2;
const float r0 = ((n1 - n2)/(n1 + n2))*((n1 - n2)/(n1 + n2));
const float M_PI = 3.1415926535897932384626433832795;
const float epsilon = 0.00001; //not really epsilon
const float gamma = 1.0/2.2;

varying vec2 coords;
uniform int tick;
uniform vec2 dims;
uniform vec3 eye;
uniform vec3 spherePositions[sphereCount];
uniform vec3 sphereAttrs[sphereCount];
uniform vec3 sphereMats[sphereCount];
uniform vec3 sphereColors[sphereCount];
uniform sampler2D fbTex;

float tick_f = float(tick);

struct Sphere {
  vec3 origin;
  vec3 attrs;
  vec3 color;
  vec3 material;
};

struct Ray{
  vec3 origin;
  vec3 dir;
};

struct Hit{
  Ray ray;
  vec3 emmittance;
  vec3 reflectance;
  int index;
};

float rand(vec2 co){
  float a = 12.9898;
  float b = 78.233;
  float c = 43758.5453;
  float dt= dot(co ,vec2(a,b) + tick_f*0.0194161103873);
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

float checkSphereCollision(Ray ray,Sphere s){
  float scalar = dot(ray.dir,ray.origin - s.origin);
  float dist = distance(ray.origin, s.origin);
  float squared = (scalar * scalar) - (dist * dist) + (s.attrs.r * s.attrs.r);
  return squared < 0.0 ? max_t : -scalar - sqrt(squared);
}

Hit getSpecular(int i, float t, Ray ray, Sphere s){
  Hit result;
  result.ray.origin = ray.dir*t + ray.origin;
  vec3 normal = normalize(result.ray.origin - s.origin);
  result.index = i;
  normal = randomVec(normal, result.ray.origin, s.material.y);
  result.ray.dir = reflect(ray.dir,normal);
  result.reflectance = vec3(0.8313,0.6863,0.2157);
  if(dot(result.ray.dir,normal) < 0.0){
    result.ray.dir = -result.ray.dir;
  }
  return result;
}

Hit getLambertian(int i, float t, Ray ray, Sphere s){
  Hit result;
  result.ray.origin = ray.dir*t + ray.origin;
  vec3 normal = normalize(result.ray.origin - s.origin);
  result.emmittance = s.attrs.z * s.color;
  result.index = i;
  result.ray.dir = randomVec(normal, result.ray.origin, 0.5);
  result.reflectance = s.color;
  return result;
}

Hit getTransmissive(int i, float t, Ray ray, Sphere s){
  Hit result;
  result.ray.origin = ray.dir*t + ray.origin;
  vec3 normal = normalize(result.ray.origin - s.origin);
  result.index = i;
  float dh = 1.0 - dot(-ray.dir,normal);
  float re = r0 + (1.0 - r0)*dh*dh*dh*dh*dh;
  if(rand(result.ray.origin.xy) < re){
    result.ray.dir = reflect(ray.dir, normal);
  }else{
    float c = dot(ray.dir,-normal);
    vec3 ref = normalize(sr*ray.dir + (sr*c - sqrt(1.0 - sr*sr*(1.0 - c*c)))*normal);
    result.ray.origin = ref*dot(ref,-normal)*s.attrs.r*2.0 + result.ray.origin;
    result.ray.dir = reflect(-ray.dir,ref);
  }
  result.reflectance = vec3(1);
  return result;
}

Hit getCollision(Ray ray, int current){
  float t = max_t;
  int mat = -1;
  Hit result;
  for(int i=0; i<sphereCount; i++){
    Sphere s = Sphere(spherePositions[i],sphereAttrs[i],sphereColors[i],sphereMats[i]);
    float nt = checkSphereCollision(ray,s);
    if(nt < t && nt > 0.0 && current != i){
      t = nt;
      mat = int(s.material.z);
      if( int(s.material.z) == 0 ){ //diffuse
        result = getLambertian(i, t, ray,s);
      } else if( int(s.material.z) == 1 ){ //specular
        result = getSpecular(i, t, ray,s);
      } else if( int(s.material.z) == 2 ){ //transmissive
        result = getTransmissive(i, t, ray,s);
      }
    }
  }
  return result;
}

void main(void) {
  float inv_dim = 1.0 / dims.x;
  vec3 tcolor = texture2D(fbTex,gl_FragCoord.xy*inv_dim).rgb;
  vec2 dof = vec2(rand(coords), rand(coords.yx))*inv_dim;
  vec3 origin = vec3(coords.x*dims.x*inv_dim + dof.x,coords.y + dof.y,0);
  Ray ray = Ray(origin,normalize(origin - eye));
  int index = -1;
  vec3 color = vec3(0,0,0);
  //No recursion in GLSL
  Hit r0 = getCollision(ray,index);
  Hit r1 = getCollision(r0.ray,r0.index);
  Hit r2 = getCollision(r1.ray,r1.index);
  Hit r3 = getCollision(r2.ray,r2.index);
  Hit r4 = getCollision(r3.ray,r3.index);
  Hit r5 = getCollision(r4.ray,r4.index);
  Hit r6 = getCollision(r5.ray,r5.index);
  color = (r0.emmittance + r0.reflectance *
          (r1.emmittance + r1.reflectance *
          (r2.emmittance + r2.reflectance *
          (r3.emmittance + r3.reflectance *
          (r4.emmittance + r4.reflectance *
          (r5.emmittance + r5.reflectance *
          (r6.emmittance)))))));
  color = pow(color,vec3(gamma));

  gl_FragColor = vec4((color + (tcolor * tick_f))/(tick_f+1.0),1.0);
}
