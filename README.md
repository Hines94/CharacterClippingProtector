# Character Clipping Protector
A unity tool to reduce the amount of clipping between clothing layers and characters by hiding areas of the mesh which are occluded by outer layers.

<img src="https://github.com/Hines94/ImagesForRepos/blob/master/PreClipper.png" width="310" height="266"> | <img src="https://github.com/Hines94/ImagesForRepos/blob/master/ClipperRun.png" width="310" height="266">

Works very well, small issues with certain areas such as armpits as described below.

### How To Setup:
- Download the project

- Open the Scene "Character Clipping Test Scene"

- Reset the meshes on the occlusion pawn to default

- Run the "Character Clipping Protector" - Check button - on "HumanMesh_WITHCLIPPINGPROTECTOR"

- Adjust the sliders for differing results

- Requires the packages: Burst, Jobs & Editor Coroutines

### Strengths:
- Fast - could be potentially used sparingly realtime depending on the mesh

- Good coverage - Gets most areas and flat/slightly rounded surfaces

### Weaknesses:
- Complex areas - Areas such as the armpits/ inner thighs can be an issue due to the huge variance in angles

- For large meshes will cause perforance isues

- Async- must be run in a coroutine

- Only setup for skinned meshes

### Usage:

- DO NOT RUN in any pose other than T or A pose - will get varying results

- In editor use the "check" option as described above to run the simulation

- To run from code simply set the meshes and then start the RunClippingSimulation coroutine (with optional callback when complete)

- Adjust margin to add "safety" margins where the edges of the mesh occur

### Tips:
- ORDER MESHES in terms of layer with the lowest priority first in the inspector (see example)

- Split meshes up if required: The script will do a reasonable job on all areas but manual culling could be required in certain situations (split arms/legs/torso/head etc)

- Calculate all mesh combinations upfront in a loading phase and store the results with the "Caching" feature - will then allow quick culling later during gameplay

### Method:
- Creates a seperate collider mesh to test against for each mesh

- Uses the new Raycast command to cast for each vertex on each mesh from the outside in
 
- Collects results and processes to find areas with overlap

- Creates a new mesh and hides areas with signigicant overlap

### Debugging:
- Use the debug options on the tool to show either mesh hits (overlapping zones) or every raycast made.

<img src="https://github.com/Hines94/ImagesForRepos/blob/master/ClipperRunning.png" width="310" height="266">
