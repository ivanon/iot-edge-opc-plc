#!/usr/bin/env python3
# Test calling an OPC UA Method by NodeId string.
# Usage: python test_settemperature.py <MethodNodeId> [targetTemp]
# Example: python test_settemperature.py Fermenter.F11.Temperature.SetSP 42.5
import sys
from opcua import Client, ua

URL = "opc.tcp://127.0.0.1:50000"
IDX = 3


def extract_fermenter_name(method_node_id: str) -> str:
    """从类似 Fermenter.F11.Temperature.SetSP 的 NodeId 中提取发酵罐名（如 F11）。"""
    parts = method_node_id.split(".")
    if len(parts) >= 2 and parts[0] == "Fermenter":
        return parts[1]
    raise ValueError(f"无法从 NodeId '{method_node_id}' 中提取发酵罐名称，期望格式: Fermenter.<罐号>...")


def find_method_parent_by_references(client, method_node):
    """通过 Browse 方法节点的 Inverse HasComponent 引用找到正确的 Object 父节点。"""
    nodeid = method_node.nodeid
    # 使用 Browse 获取该节点的所有引用
    descs = client.browse(nodeid)
    for ref in descs:
        # IsForward=False 表示指向该节点的引用（即父节点）
        if not ref.IsForward:
            ref_type = ref.ReferenceTypeId
            # HasComponent (i=47) 或 Organizes (i=35) 都可以作为 parent
            if ref_type.Identifier in (47, 35):
                return client.get_node(ref.NodeId)
    return None


def find_parent_by_nodeid_path(client, method_node_id_str: str):
    """根据 NodeId 的点分路径推断 parent 的 BrowsePath 并获取节点。
    例如 Fermenter.F11.Temperature.SetSP -> parent path = Fermenter/F11/Temperature
    """
    parts = method_node_id_str.split(".")
    if len(parts) <= 1:
        return None
    parent_path_parts = parts[:-1]
    # 组装 browse path: ["3:Fermenter", "3:F11", "3:Temperature"]
    browse_names = [f"{IDX}:{p}" for p in parent_path_parts]
    objects = client.get_objects_node()
    try:
        # Objects 下面通常还有一层 OpcPlc，但 opcua 的 get_child 支持多级路径
        # 先试 Objects -> ...
        return objects.get_child(browse_names)
    except Exception:
        pass
    # 再试 Objects/OpcPlc -> ...
    try:
        return objects.get_child([f"{IDX}:OpcPlc"] + browse_names)
    except Exception:
        pass
    return None


def main():
    if len(sys.argv) < 2:
        print(f"Usage: {sys.argv[0]} <MethodNodeId> [targetTemp]")
        print(f"Example: {sys.argv[0]} Fermenter.F11.Temperature.SetSP 42.5")
        sys.exit(1)

    method_node_id_str = sys.argv[1]
    target_temp = float(sys.argv[2]) if len(sys.argv) > 2 else 42.5

    fermenter_name = extract_fermenter_name(method_node_id_str)
    print(f"Method NodeId: {method_node_id_str}")
    print(f"Fermenter: {fermenter_name}, TargetTemp: {target_temp}")

    client = Client(URL)
    try:
        client.connect()
        print("Connected to OPC UA server")

        # Read current SP value before method call
        sp_node_id = f"ns={IDX};s=Fermenter.{fermenter_name}.Temperature.SP"
        sp_node = client.get_node(sp_node_id)
        old_value = sp_node.get_value()
        print(f"{sp_node_id} before call: {old_value}")

        # Resolve the method node
        method_node = client.get_node(f"ns={IDX};s={method_node_id_str}")
        print(f"Resolved method: {method_node.nodeid}")

        # 策略1: get_parent()
        parent = method_node.get_parent()
        print(f"Strategy 1 - get_parent(): {parent}")

        # 策略2: 如果策略1的 parent 看起来不对（比如返回的是 Objects 或 None），用 references
        if parent is None:
            parent = find_method_parent_by_references(client, method_node)
            print(f"Strategy 2 - find by inverse references: {parent}")

        # 策略3: 根据 NodeId 路径推导 parent
        if parent is None:
            parent = find_parent_by_nodeid_path(client, method_node_id_str)
            print(f"Strategy 3 - find by NodeId path: {parent}")

        if parent is None:
            print("ERROR: 无法确定方法所属的对象节点 (objectId)，请检查 NodeId 是否正确")
            sys.exit(1)

        result = parent.call_method(method_node, fermenter_name, target_temp)
        print(f"Method call result: {result}")

        new_value = sp_node.get_value()
        print(f"{sp_node_id} after call: {new_value}")

        if new_value == target_temp:
            print("SUCCESS: Method correctly updated the SP value!")
        else:
            print(f"FAILURE: Expected {target_temp} but got {new_value}")

    except Exception as e:
        print(f"ERROR: {e}")
        import traceback
        traceback.print_exc()
    finally:
        client.disconnect()
        print("Disconnected")


if __name__ == "__main__":
    main()
