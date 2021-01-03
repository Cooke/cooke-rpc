// Functions for use with JSON.stringify and JSON.parse to support including/extracting type information between JSON (according to 5 in https://tech.signavio.com/2017/json-type-information) and JS Objects.
// The type information in JSON is in a wrapping object that has a single key starting with '$' (for instance: { "$MyType": { "prop": "hello" } }).
// The type information in JS (after parsing JSON) is put in a property name '$type'. 
// For instance parsing JSON '{ "$MyType": { "prop": "hello" } }' would give an object { $type: 'MyType', prop: "hello" }.
// For instance stringifying the JS object { $type: 'MyType', prop: "hello" } would give back JSON '{ "$MyType": { "prop": "hello" } }'.

export const typedJsonReviver: (
  this: any,
  key: string,
  value: any
) => any = function (key, value) {
  if (value && typeof value === "object" && typeof value.$value === "object") {
    return value.$value;
  } else if (
    typeof value === "object" &&
    typeof this === "object" &&
    !Array.isArray(this) &&
    !Array.isArray(value) &&
    key.startsWith("$")
  ) {
    value.$type = key.substr(1);
    this.$value = value;
    return value;
  }

  return value;
};

export const typedJsonReplacer: (
  this: any,
  key: string,
  value: any
) => any = function (key, value) {
  if (Array.isArray(value)) {
    return value;
  }

  if (typeof value === "object" && value["$type"]) {
    const clone = { ...value };
    delete clone["$type"];
    return {
      ["$" + value["$type"]]: clone,
    };
  }

  return value;
};
