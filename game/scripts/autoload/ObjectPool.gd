extends Node
## Generic node pool.
##
## Pooled nodes are created once, parented under `container` (a node inside the
## level so they share the world transform) and toggled on/off instead of being
## instantiated and freed every shot. A pooled node may implement
## `_on_pool_release()` for cleanup.

var container: Node = null
var _free: Dictionary = {}       # key -> Array[Node]
var _factories: Dictionary = {}  # key -> Callable
var _active_count: Dictionary = {}


func register(key: StringName, factory: Callable) -> void:
	_factories[key] = factory
	if not _free.has(key):
		_free[key] = []
		_active_count[key] = 0


func prewarm(key: StringName, n: int) -> void:
	var made: Array = []
	for i in n:
		made.append(acquire(key))
	for m in made:
		release(m)


func acquire(key: StringName) -> Node:
	var list: Array = _free[key]
	var node: Node
	while not list.is_empty():
		node = list.pop_back()
		if is_instance_valid(node):
			break
		node = null
	if node == null:
		node = _factories[key].call()
		node.set_meta("pool_key", key)
		container.add_child(node)
	node.set_meta("pooled", false)
	node.process_mode = Node.PROCESS_MODE_INHERIT
	if node is CanvasItem:
		node.visible = true
	_active_count[key] += 1
	return node


func release(node: Node) -> void:
	if not is_instance_valid(node) or node.get_meta("pooled", false):
		return
	node.set_meta("pooled", true)
	if node.has_method("_on_pool_release"):
		node._on_pool_release()
	if node is CanvasItem:
		node.visible = false
	node.process_mode = Node.PROCESS_MODE_DISABLED
	var key: StringName = node.get_meta("pool_key")
	_active_count[key] -= 1
	_free[key].append(node)


func active(key: StringName) -> int:
	return _active_count.get(key, 0)


## Called when a level is torn down: the container frees the nodes.
func reset(new_container: Node) -> void:
	container = new_container
	for k in _free.keys():
		_free[k] = []
		_active_count[k] = 0
