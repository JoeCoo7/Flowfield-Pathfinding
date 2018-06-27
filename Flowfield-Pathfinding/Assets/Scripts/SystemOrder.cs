using Unity.Entities;

//----------------------------------------------------------------------------------------
public class InputGroup {}
// InputSystem

//----------------------------------------------------------------------------------------
[UpdateAfter(typeof(InputGroup))]
public class ProcessGroup {}
// AgentSpawningSystem
// AgentSystem
// TileSystem
// UpdateAgentWithQuerySystem


//----------------------------------------------------------------------------------------
[UpdateAfter(typeof(ProcessGroup))]
public class RenderingGroup {}
// TileMeshInstanceRendererSystem
// RenderSystem
