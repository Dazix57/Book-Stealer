using UnityEngine;

// Marca un GameObject como punto de patrullaje. EnemyController recolecta automáticamente
// todos los que haya en la escena (ver FindObjectsByType en Awake), en vez de necesitar que
// cada enemigo los tenga asignados a mano en el Inspector.
public class PatrolWaypoint : MonoBehaviour { }
