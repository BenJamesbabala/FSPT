(function (exports) {
  exports.BVH = class BVH {
    constructor(triangles, maxTris){
      this.root = this.buildTree(triangles, maxTris);
    }

    buildTree(triangles, maxTris) {
      let root = new Node(triangles);
      let split = root.getSplittingAxis();
      root.sortOnAxis(split);
      if (root.triangles.length <= maxTris) {
        root.leaf = true;
        return root;
      }
      let l = root.triangles.length;
      let i = root.getSplittingIndex(split);
      root.left = this.buildTree(triangles.slice(0, i), maxTris);
      root.right = this.buildTree(triangles.slice(i, l), maxTris);
      root.triangles = null;
      return root;
    }

    serializeTree() {
      let nodes = [],
        i = -1;

      function traverseTree(root, prev) {
        let parent = ++i;
        let node = {node: root, parent: prev};
        nodes.push(node);
        if (!root.leaf) {
          node.left = traverseTree(root.left, parent);
          node.right = traverseTree(root.right, parent);
          nodes[node.left].sibling = node.right;
          nodes[node.right].sibling = node.left;
        }
        return parent
      }

      traverseTree(this.root, i);
      return nodes;
    }
  };

  class BoundingBox {
    constructor(triangles) {
      let tris = Array.isArray(triangles) ? triangles : [triangles];
      this._box = [Infinity, -Infinity, Infinity, -Infinity, Infinity, -Infinity];
      for (let j = 0; j < tris.length; j++) {
        let vals = tris[j].v1.concat(tris[j].v2, tris[j].v3);
        for (let i = 0; i < vals.length; i++) {
          this._box[(i % 3) * 2] = Math.min(vals[i], this._box[(i % 3) * 2]);
          this._box[(i % 3) * 2 + 1] = Math.max(vals[i], this._box[(i % 3) * 2 + 1]);
        }
      }
      this._centroid = [
        (this._box[0] + this._box[1]) / 2,
        (this._box[2] + this._box[3]) / 2,
        (this._box[4] + this._box[5]) / 2
      ];
    }

    getBounds() {
      return this._box;
    }

    getCenter(axis) {
      return this._centroid[axis];
    }
  }

  class Node {
    constructor(triangles){
      this.triangles = triangles;
      this.boundingBox = new BoundingBox(triangles);
      this.leaf = false;
      this.left = null;
      this.right = null;
      this.split = null;
    }

    getSplittingAxis () {
      let box = this.boundingBox.getBounds();
      let bestIndex = 0;
      let bestSpan = 0;
      for (let i = 0; i < box.length / 2; i++) {
        let span = Math.abs(box[i * 2] - box[i * 2 + 1]);
        if (span > bestSpan) {
          bestSpan = span;
          bestIndex = i;
        }
      }
      return bestIndex;
    }

    getSplittingIndex(axis) {
      let median = this.boundingBox.getCenter(axis);
      for (let i = 0; i < this.triangles.length; i++) {
        let point = this.triangles[i].boundingBox.getCenter(axis)
        if (point > median) {
          return i;
        }
      }
      return this.triangles.length / 2;
    }

    sortOnAxis(axis) {
      this.split = axis;
      this.triangles.sort(function (t1, t2) {
        let c1 = t1.boundingBox.getCenter(axis);
        let c2 = t2.boundingBox.getCenter(axis);
        if (c1 < c2) {
          return -1;
        }
        if (c1 > c2) {
          return 1;
        }
        return 0;
      });
    }
  }

  class Triangle {
    constructor(v1, v2, v3, i1, i2, i3, uv1, uv2, uv3, transforms) {
      this.v1 = v1;
      this.v2 = v2;
      this.v3 = v3;
      this.i1 = i1;
      this.i2 = i2;
      this.i3 = i3;
      this.normals = null;
      this.uv1 = uv1;
      this.uv2 = uv2;
      this.uv3 = uv3;
      this.boundingBox = new BoundingBox(this);
      this.transforms = transforms;
    }
  }

  // function splitTriangle(triangle, threshold){
  // let bb = triangle.boundingBox.getBounds();
  // let span = 0;
  // for(let i = 0; i< bb.length/2; i++){
  // span = Math.max(bb[i*2+1] - bb[i*2], span)
  // }
  // if(span < threshold){
  // return [triangle]
  // }

  // let verts = [triangle.v1, triangle.v2, triangle.v3];
  // let idx;
  // let scalar = 1;
  // for(let i=0; i<verts.length; i++){
  // let left = verts[(i+2) % verts.length];
  // let right = verts[(i+1) % verts.length];
  // let e1 = normalize(sub(left, verts[i]));
  // let e2 = normalize(sub(right, verts[i]));
  // let tempScalar = dot(e1, e2);
  // if(tempScalar < scalar){
  // idx = i;
  // scalar = tempScalar;
  // }
  // }

  // let opposite = scale(add(verts[(idx+2) % verts.length], verts[(idx+1) % verts.length]),0.5);
  // let t1 = new Triangle(verts[idx],verts[(idx+1) % verts.length], opposite, triangle.transforms);
  // let t2 = new Triangle(verts[idx], opposite, verts[(idx+2) % verts.length], triangle.transforms);
  // return splitTriangle(t1, threshold).concat(splitTriangle(t2, threshold));
  // }

  exports.Triangle = Triangle;
})(this);
